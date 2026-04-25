# aggregator.py
import socket
import time
import math

# ---- config ----
IN_PORTS = [52833, 52834, 52835, 52836]   # aggregator input ports
OUT_ADDR = ("127.0.0.1", 53000)           # Unity collective listens here
STALE_SEC = 0.5
TARGET_HZ = 30

JOINT_COUNT = 10

def parse_mprot(payload: str):
    lines = payload.strip().splitlines()
    if not lines or lines[0].strip() != "mprot":
        return None

    out = {}
    for ln in lines[1:]:
        parts = ln.split("|")
        if len(parts) != 6:
            continue
        j = int(parts[0])
        qx, qy, qz, qw = map(float, parts[1:5])
        vis = float(parts[5])
        out[j] = (qx, qy, qz, qw, vis)
    return out

def quat_dot(a, b):
    return a[0]*b[0] + a[1]*b[1] + a[2]*b[2] + a[3]*b[3]

def quat_norm(q):
    return math.sqrt(quat_dot(q, q))

def quat_normalize(q):
    n = quat_norm(q)
    if n <= 1e-8:
        return (0.0, 0.0, 0.0, 1.0)
    return (q[0]/n, q[1]/n, q[2]/n, q[3]/n)

def quat_neg(q):
    return (-q[0], -q[1], -q[2], -q[3])

def average_quats(quats):
    if not quats:
        return (0.0, 0.0, 0.0, 1.0)

    q_ref = quats[0]
    sx = sy = sz = sw = 0.0
    for q in quats:
        if quat_dot(q, q_ref) < 0.0:
            q = quat_neg(q)
        sx += q[0]
        sy += q[1]
        sz += q[2]
        sw += q[3]
    return quat_normalize((sx, sy, sz, sw))

def build_mprot(joint_map):
    lines = ["mprot"]
    for j in range(JOINT_COUNT):
        qx, qy, qz, qw, vis = joint_map.get(j, (0.0, 0.0, 0.0, 1.0, 0.0))
        lines.append(f"{j}|{qx}|{qy}|{qz}|{qw}|{vis}")
    return "\n".join(lines) + "\n"

def main():
    in_socks = []
    for p in IN_PORTS:
        s = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
        s.bind(("0.0.0.0", p))
        s.setblocking(False)
        in_socks.append((p, s))
        print(f"[agg] listening on UDP {p}")

    out_sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
    print(f"[agg] sending to {OUT_ADDR[0]}:{OUT_ADDR[1]}")

    latest = {}  # port -> (timestamp, joint_dict)
    last_send = 0.0
    min_period = 1.0 / max(1, TARGET_HZ)

    while True:
        now = time.time()

        for port, s in in_socks:
            while True:
                try:
                    data, addr = s.recvfrom(65535)
                except BlockingIOError:
                    break
                except Exception as e:
                    print(f"[agg] recv error on {port}: {e}")
                    break

                try:
                    txt = data.decode("utf-8", errors="ignore")
                except Exception:
                    continue

                joints = parse_mprot(txt)
                if joints is not None:
                    latest[port] = (now, joints)

        if now - last_send < min_period:
            time.sleep(0.001)
            continue

        active = []
        for port, (ts, joints) in list(latest.items()):
            if now - ts <= STALE_SEC:
                active.append((port, joints))

        fused = {}
        if active:
            for j in range(JOINT_COUNT):
                quats = []
                vises = []
                for port, joints in active:
                    if j in joints:
                        qx, qy, qz, qw, vis = joints[j]
                        quats.append((qx, qy, qz, qw))
                        vises.append(vis)
                q_avg = average_quats(quats)
                vis_avg = sum(vises) / len(vises) if vises else 0.0
                fused[j] = (*q_avg, vis_avg)

        payload = build_mprot(fused)
        out_sock.sendto(payload.encode("utf-8"), OUT_ADDR)
        last_send = now

if __name__ == "__main__":
    main()