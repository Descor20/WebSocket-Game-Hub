let socket;

export async function connect(url, dotNetRef) {
    console.log("connect() appelée avec url:", url);
    socket = new WebSocket(url);
    socket.onopen = async () => {
        console.log("WebSocket ouvert !");
        const _ = await dotNetRef.invokeMethodAsync("OnConnected");
    };
    socket.onerror = async (err) => {
        console.error("Erreur WebSocket:", err);
        const _ = await dotNetRef.invokeMethodAsync("OnError", err?.message ?? "Erreur inconnue");
    };
    socket.onclose = async (e) => {
        console.log("WebSocket fermé:", e.code, e.reason);
        const _ = await dotNetRef.invokeMethodAsync("OnDisconnected", e.code, e.reason);
    };
    socket.onmessage = async (event) => {
        console.log("WebSocket message:", event.data);
        try {
            await dotNetRef.invokeMethodAsync("OnMessageReceived", event.data);
        } catch (err) {
            // Intercepte l'erreur si C# rejette la promesse
            console.error("Erreur lors de l'exécution C# :", err);
        }
    };
}

export function sendMessage(msg) {
    if (!socket || socket.readyState !== WebSocket.OPEN) {
        console.error("Impossible d'envoyer : socket non connecté");
        return;
    }
    try {
        socket.send(JSON.stringify(msg));
    } catch (err) {
        console.error("Erreur lors de l'envoi:", err);
    }
}

export function testDisplay(message) {
    const date = new Date();
    console.log(date.toLocaleDateString("en-US") + message);
}

export function disconnect() {
    socket?.close();
}