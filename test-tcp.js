const net = require('net');

async function testConnection(host, port) {
    return new Promise((resolve) => {
        const socket = new net.Socket();
        socket.setTimeout(500); // 500ms timeout
        
        socket.on('connect', () => {
            socket.destroy();
            resolve(true);
        });
        
        socket.on('timeout', () => {
            socket.destroy();
            resolve(false);
        });
        
        socket.on('error', () => {
            socket.destroy();
            resolve(false);
        });
        
        socket.connect(port, host);
    });
}

testConnection('172.22.144.1', 50051).then(console.log);
