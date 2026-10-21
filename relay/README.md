# Encrypted MiniMap relay

## Host member management (v2.3.19)

Updated clients use `SCUMGROUP2 <group ID> <room ID> <64 hex connection ID>
<base64 encrypted member label> [owner token]` over the same pinned TLS connection.
The connection ID is random on every connection, independent of player names,
positions and member colors. The owner token is never forwarded to members.
The label is the existing authenticated encryption of `member-name-v1:<name>`
with the group key, so the relay cannot read it. It is held in memory only.

On a separate pinned TLS connection, `SCUMMEMBERS2 LIST <group ID> <room ID>
<owner token> <64 zeroes>` returns `OK`, followed by up to nine rows of
`<connection ID> <owner flag 0/1> <encrypted label>`, and `END`, each on its own
line. `SCUMMEMBERS2 KICK <group ID> <room ID> <owner token> <connection ID>`
disconnects that exact live connection and acknowledges `OK`. Missing targets
and protected owner connections return `NO`. Other groups and connections stay
connected. There is no ban, membership registry or code change: the member can
rejoin with the same invite code. The UI polls this live list every three seconds
only while the host opens Manage members. It does not claim success on a failed
request. Existing clients retain GROUP1 support; their names cannot be associated
by the opaque relay, so the host sees temporary connection IDs instead. The
host and newer clients should update to display names and protect the host row.
The durable registry and traffic ledger formats remain unchanged.

Node.js 22, TypeScript ES2022, no runtime dependencies. Build with TypeScript and
Node types installed in the companion workspace:

```powershell
node ../../../ScumServer_Companion/node_modules/typescript/bin/tsc server.ts --target ES2022 --module commonjs --strict --types node --typeRoots ../../../ScumServer_Companion/node_modules/@types --outDir dist --skipLibCheck
node --test test.cjs
```

Run commands from this folder. Transport tests also need `TEST_KEY` and
`TEST_CERT` pointing to a synthetic TLS fixture. They skip without those variables.
`tests/Test-RelayIntegration.ps1` in the application runs real C# clients against
the configured service; use `-Address` and `-Port` for a local SSH test tunnel.

On Linux, install compiled `dist/server.js`, a TLS certificate (`server.crt`) and
its root-readable key (`server.key`) in `/opt/minimap-relay`. Install the supplied
systemd unit in `/etc/systemd/system`. It loads the private key with systemd
credentials and runs with a dynamic unprivileged user, filesystem restrictions,
128 MB memory and a 20% CPU quota. Initialize `budget.json` explicitly in its
`StateDirectory` before first start with the current UTC month and zero reserved
bytes: `{"month":"YYYY-MM","reserved":0}`. Also explicitly initialize `groups.json`
with `{"version":1,"groups":{}}` on first installation. Never reset or remove a
live ledger or group registry. Both use atomic, flushed writes; invalid state fails closed.
Missing, corrupt or unwritable state fails closed. Preserve the certificate/key
across deployments; certificate rotation needs a matching client pin update.

Allow stateful TCP ingress on port 47832 in Oracle and the VM firewall. Preserve
SSH and other services. The client pins SHA-256 of the DER certificate; a public
CA/domain is unnecessary. Updating the pin requires a new application build.

Protocol: TLS 1.2+, then `SCUMGROUP1 <32 lowercase hex group ID> <64 lowercase hex room ID>\n`.
The server acknowledges `OK\n`. Each subsequent frame is a 4-byte unsigned
big-endian length followed by the unchanged encrypted position-protocol-3
packet (64..131072 bytes). Room ID = HMAC-SHA256(group key, UTF-8
`SCUM MiniMap opaque relay room v1`). The relay cannot derive the group key.
Echoing each sender's frame keeps solitary clients alive; clients ignore their
own publications. The legacy ephemeral join is disabled on the production service.
No encryption keys, packets, names or coordinates are logged/saved.

Owner control uses a separate TLS connection: `SCUMCREATE1 <group ID> <room ID>
<64 hex owner token>\n`, `SCUMROTATE1 ...` or `SCUMDELETE1 ...`, acknowledged with `OK\n` only after
the registry is flushed. Registry entries contain only the room identifier and
SHA-256 of `MiniMap owner v1:<group ID>:<owner token>`. Rotating disconnects old-room
members, blocks the retired code, and leaves other groups connected. Deleting
disconnects every member of that group and permanently blocks its codes. A
`deleted: true` tombstone retains only the existing room and owner hashes;
CREATE/ROTATE cannot revive it, while owner DELETE retries are idempotent.
Other groups and the traffic ledger remain unchanged. Idempotent
retries recover interrupted owner operations. The client durably journals a pending
create/refresh, encrypted with Windows DPAPI, before contacting the service.
Room identifiers are namespaced by group ID. The registry persists across restart
and has no inactivity expiry; empty room sockets are removed immediately.
At most 10,000 groups can be registered. No public group directory is provided.

Limits: 64 TCP connections, 12 per IP, 32 rooms, nine members per room, three
frames and 256 KiB input per connection per second, 256 KiB receive/write buffers,
10-second idle timeout, 5-second TLS handshake, 8 MiB/s aggregate conservative
output accounting. Slow/invalid clients are disconnected. A room identifier
grants routing access, so TLS protects it in transit; packet authentication still
requires the independent group key. The service does not authenticate Steam users.

Default budget: 50,000,000,000 accounted bytes per UTC month. Reserve 32 KiB per
TCP admission for handshake overhead and twice frame bytes plus 256 per recipient.
Persist reservations in 1 MiB blocks with fsync and atomic rename before sending.
Restart forfeits unused reservations. Clock rollback locks the ledger. The cap
does not meter unrelated VM/tenancy traffic, kernel traffic or provider billing.
Account limits must still cover all workloads; budget alerts are not hard caps.

Inspect with `systemctl status minimap-relay`, `journalctl -u minimap-relay`, and
the `budget.json` state file. Stop with `systemctl stop minimap-relay`. Existing
direct sharing remains usable independently. No load balancer, paid storage,
managed database, third-party relay subscription or paid failover is needed.
