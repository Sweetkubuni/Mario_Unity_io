use std::collections::HashMap;
use std::net::SocketAddr;
use std::sync::atomic::{AtomicU32, Ordering};
use std::sync::Arc;

use futures_util::{SinkExt, StreamExt};
use serde::{Deserialize, Serialize};
use tokio::net::{TcpListener, TcpStream};
use tokio::sync::{mpsc, Mutex};
use tokio_tungstenite::tungstenite::Message;

const BIND_ADDR: &str = "0.0.0.0:8765";
const MAX_USERNAME_LEN: usize = 16;

#[derive(Deserialize)]
#[serde(tag = "type", rename_all = "snake_case")]
enum ClientMsg {
    Join {
        username: String,
    },
    State {
        x: f32,
        y: f32,
        z: f32,
        ry: f32,
        speed: f32,
        vspeed: f32,
        grounded: bool,
    },
}

#[derive(Serialize, Clone)]
struct PlayerInfo {
    id: u32,
    username: String,
    x: f32,
    y: f32,
    z: f32,
    ry: f32,
}

#[derive(Serialize)]
#[serde(tag = "type", rename_all = "snake_case")]
enum ServerMsg {
    Welcome {
        id: u32,
        players: Vec<PlayerInfo>,
    },
    PlayerJoined {
        id: u32,
        username: String,
    },
    PlayerLeft {
        id: u32,
    },
    State {
        id: u32,
        x: f32,
        y: f32,
        z: f32,
        ry: f32,
        speed: f32,
        vspeed: f32,
        grounded: bool,
    },
    Error {
        message: String,
    },
}

#[derive(Default, Clone, Copy)]
struct LastState {
    x: f32,
    y: f32,
    z: f32,
    ry: f32,
}

struct Peer {
    username: String,
    tx: mpsc::UnboundedSender<Message>,
    last_state: LastState,
}

type Room = Arc<Mutex<HashMap<u32, Peer>>>;

fn to_msg(msg: &ServerMsg) -> Message {
    Message::Text(serde_json::to_string(msg).expect("serialize"))
}

async fn broadcast(room: &Room, msg: &ServerMsg, except: Option<u32>) {
    let text = to_msg(msg);
    let peers = room.lock().await;
    for (id, peer) in peers.iter() {
        if Some(*id) == except {
            continue;
        }
        let _ = peer.tx.send(text.clone());
    }
}

async fn handle_connection(stream: TcpStream, addr: SocketAddr, room: Room, next_id: Arc<AtomicU32>) {
    let ws = match tokio_tungstenite::accept_async(stream).await {
        Ok(ws) => ws,
        Err(e) => {
            eprintln!("[{addr}] websocket handshake failed: {e}");
            return;
        }
    };
    let (mut sink, mut source) = ws.split();

    // Outgoing messages are funneled through a channel so the room can push
    // to this client while we block on reads.
    let (tx, mut rx) = mpsc::unbounded_channel::<Message>();
    let writer = tokio::spawn(async move {
        while let Some(msg) = rx.recv().await {
            if sink.send(msg).await.is_err() {
                break;
            }
        }
        let _ = sink.close().await;
    });

    // First message must be a valid join.
    let my_id = loop {
        let msg = match source.next().await {
            Some(Ok(Message::Text(text))) => text,
            Some(Ok(Message::Ping(_) | Message::Pong(_))) => continue,
            _ => {
                writer.abort();
                return;
            }
        };
        let username = match serde_json::from_str::<ClientMsg>(&msg) {
            Ok(ClientMsg::Join { username }) => username.trim().to_string(),
            _ => {
                let _ = tx.send(to_msg(&ServerMsg::Error {
                    message: "expected join message".into(),
                }));
                writer.abort();
                return;
            }
        };
        if username.is_empty() || username.chars().count() > MAX_USERNAME_LEN {
            let _ = tx.send(to_msg(&ServerMsg::Error {
                message: format!("username must be 1-{MAX_USERNAME_LEN} characters"),
            }));
            continue;
        }

        let mut peers = room.lock().await;
        if peers
            .values()
            .any(|p| p.username.eq_ignore_ascii_case(&username))
        {
            drop(peers);
            let _ = tx.send(to_msg(&ServerMsg::Error {
                message: "username already taken".into(),
            }));
            continue;
        }

        let id = next_id.fetch_add(1, Ordering::Relaxed);
        let players: Vec<PlayerInfo> = peers
            .iter()
            .map(|(pid, p)| PlayerInfo {
                id: *pid,
                username: p.username.clone(),
                x: p.last_state.x,
                y: p.last_state.y,
                z: p.last_state.z,
                ry: p.last_state.ry,
            })
            .collect();
        peers.insert(
            id,
            Peer {
                username: username.clone(),
                tx: tx.clone(),
                last_state: LastState::default(),
            },
        );
        drop(peers);

        let _ = tx.send(to_msg(&ServerMsg::Welcome { id, players }));
        broadcast(
            &room,
            &ServerMsg::PlayerJoined {
                id,
                username: username.clone(),
            },
            Some(id),
        )
        .await;
        println!("[{addr}] '{username}' joined as #{id}");
        break id;
    };

    while let Some(msg) = source.next().await {
        let text = match msg {
            Ok(Message::Text(text)) => text,
            Ok(Message::Close(_)) | Err(_) => break,
            _ => continue,
        };
        if let Ok(ClientMsg::State {
            x,
            y,
            z,
            ry,
            speed,
            vspeed,
            grounded,
        }) = serde_json::from_str::<ClientMsg>(&text)
        {
            {
                let mut peers = room.lock().await;
                if let Some(me) = peers.get_mut(&my_id) {
                    me.last_state = LastState { x, y, z, ry };
                }
            }
            broadcast(
                &room,
                &ServerMsg::State {
                    id: my_id,
                    x,
                    y,
                    z,
                    ry,
                    speed,
                    vspeed,
                    grounded,
                },
                Some(my_id),
            )
            .await;
        }
    }

    let username = {
        let mut peers = room.lock().await;
        peers.remove(&my_id).map(|p| p.username)
    };
    broadcast(&room, &ServerMsg::PlayerLeft { id: my_id }, None).await;
    writer.abort();
    println!(
        "[{addr}] '{}' (#{my_id}) left",
        username.unwrap_or_default()
    );
}

#[tokio::main]
async fn main() {
    let room: Room = Arc::new(Mutex::new(HashMap::new()));
    let next_id = Arc::new(AtomicU32::new(1));

    let listener = TcpListener::bind(BIND_ADDR)
        .await
        .unwrap_or_else(|e| panic!("failed to bind {BIND_ADDR}: {e}"));
    println!("mario-server listening on ws://{BIND_ADDR}");

    loop {
        let (stream, addr) = match listener.accept().await {
            Ok(pair) => pair,
            Err(e) => {
                eprintln!("accept failed: {e}");
                continue;
            }
        };
        tokio::spawn(handle_connection(
            stream,
            addr,
            room.clone(),
            next_id.clone(),
        ));
    }
}
