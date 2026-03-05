import socket
import threading
import time
import errno

class ClientUDP(threading.Thread):
    """
    UDP sender.
    - Uses sendto (no UDP connect).
    - Never blocks the render loop.
    - If receiver is not running, errors are throttled (no FPS drop).
    """

    def __init__(self, ip, port, autoReconnect=True) -> None:
        super().__init__(daemon=True)
        self.ip = ip
        self.port = port
        self.autoReconnect = autoReconnect  # kept for compatibility; no real reconnect needed for UDP
        self.connected = False
        self._sock = None
        self._next_log_time = 0.0

    def run(self):
        # Keep the same behavior: start() will spawn a thread that prepares the socket.
        self.connect()

    def isConnected(self):
        return self.connected

    def connect(self):
        # For UDP, "connect" just means "create a socket and remember destination".
        if self._sock is not None:
            try:
                self._sock.close()
            except Exception:
                pass

        self._sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
        # Non-blocking so send never stalls; sendto on UDP should be fast anyway.
        self._sock.setblocking(False)
        self.connected = True
        print(f"UDP client ready. Will send messages to ({self.ip}, {self.port})")

    def disconnect(self):
        self.connected = False
        if self._sock is not None:
            try:
                self._sock.close()
            except Exception:
                pass
            self._sock = None

        # Keep autoReconnect semantics but don't sleep in the main loop.
        if self.autoReconnect:
            # Recreate socket after a short delay, but do it without blocking caller.
            threading.Thread(target=self._delayed_reconnect, daemon=True).start()

    def _delayed_reconnect(self):
        time.sleep(1.0)
        self.connect()

    def sendMessage(self, message):
        if not self.connected or self._sock is None:
            return

        payload = str(f"{message}<EOM>").encode("utf-8")

        try:
            self._sock.sendto(payload, (self.ip, self.port))
        except OSError as e:
            # macOS may raise ECONNREFUSED when the destination port is closed (ICMP unreachable).
            if e.errno in (errno.ECONNREFUSED, errno.EHOSTUNREACH, errno.ENETUNREACH):
                now = time.time()
                if now >= self._next_log_time:
                    print("UDP send failed (receiver not listening yet).")
                    self._next_log_time = now + 1.0  # throttle logs to 1/sec
                # Do NOT disconnect/reconnect on UDP errors; just ignore and keep going.
                return
            # For any other unexpected error, you may want to see it.
            raise