# SCUM MiniMap encrypted relay

Node.js 22+, TypeScript ES2022. No runtime dependencies. Run `npm ci` then
`npm test` in this directory. TLS transport tests additionally need `TEST_KEY`
and `TEST_CERT` pointing to a disposable certificate fixture. The GitHub relay
check generates its own synthetic TLS fixture and exercises all tests.

The service forwards opaque authenticated/encrypted position frames. Group codes
contain a group identifier and encryption key; the server receives only a routing
identifier derived from that key. It persists opaque group IDs, current routing
IDs and hashes of independent owner capabilities. Only the owner can rotate a
code. Rotation retires the old routing ID and disconnects its members. Empty
active rooms disappear immediately; persistent codes do not expire on inactivity.

Clients pin the server certificate. See `DirectPositions` and `RelayGroupStore`
in `src/` for the handshake and owner-operation protocol. `RelayGroupStore`
protects saved memberships and owner capabilities with Windows DPAPI, journals
owner operations before sending, and keeps sharing disabled on app restart.

A persistent monthly budget is reserved before network output. Restarting cannot
reset usage. Missing/corrupt state fails closed. Runtime inputs are TLS key/cert,
a `STATE_DIRECTORY` holding explicitly initialized `budget.json`
(`{"month":"YYYY-MM","reserved":0}`) and `groups.json`
(`{"version":1,"groups":{}}`), and optional `PORT`/`MONTHLY_BYTES`. Never recreate
a missing live ledger or registry as an automatic fallback. Default accounting
is 50 GB/month, limited to this service; it is not an account-wide billing cap.
Connection, buffer, room, rate and queue limits are enforced in `server.ts`.

Private operator credentials, systemd installation files and deployment tooling
are intentionally outside the public source archive.
