# Private positions

## Persistent private groups (default)

Open Settings > General > Private positions. A display name is filled in for you;
you can change it. Click **Create group and copy code** to create and connect to
your group. Give that code privately to your friends. They paste it into **Group
code** and click **Connect**. Nobody enters an IP, port or connection mode, and
nobody needs home port forwarding, a VPN account or a public home address.
All members need the persistent-group build. Close Settings to resume normal
game position capture.

Codes stay valid until their owner clicks **Refresh code**. A refresh disconnects
that group's existing members and makes the old code unusable. Share the new code
to admit people again. Only the creator has the separate owner credential; it is
never included in the shared group code. Keep the creator's saved Windows profile
to retain ownership. The current application stores codes and owner credentials
encrypted with Windows DPAPI in `private-groups.dat` inside its profile directory.
They are tied to that Windows account, not a portable ownership backup.

You can save up to 16 groups and connect to several at once. Each saved group has
its own Connect/Leave and Copy code controls; owned groups also show Refresh code.
Leaving disconnects you immediately and keeps the code for later. Starting the
app restores saved groups with sharing disabled. Rooms with no connected members
are removed from memory immediately; their persistent codes and ownership remain
valid. There is no seven-day inactivity expiry. The server stores only opaque
group/routing identifiers and owner-credential hashes, never decryption keys or
position history. Each group shares only your own positions and markers; joining
several groups does not forward one group's members into another group.

The configured Oracle relay uses outbound TLS connections and a pinned server
certificate. Names, positions, GPS destinations, waypoints and death markers are
encrypted and authenticated on the participants' PCs. The relay receives an
opaque room identifier derived from the key, never the key itself. It forwards
encrypted packets without decoding them or retaining location history. The relay
operator can still see connecting IP addresses, room membership, sizes and timing.
People with your group code can read and publish in your group. Refresh the code
when membership changes. Active sharing remains opt-in and disabled at startup.

Each room supports nine participants. The service has connection/rate limits and
a conservative 50 GB monthly traffic budget with an automatic cutoff. Unused
reserved traffic is forfeited on restart; restarting does not reset the budget.
The allowance renews at the start of each UTC calendar month. Reconnect by
using that group's Connect button after a failure. Direct Host/Join remains available
when relay capacity is unavailable. No paid fallback is used.

The relay's budget limits this service only. It cannot guarantee a zero Oracle
bill or account-wide availability: other workloads share the account's free
allowances, and Oracle controls infrastructure availability and billing.

## Direct Host / Join

Open Settings > General > Private positions, then Host group / Join group.
The host enters a display name,
selects Host group, chooses a TCP port (default 47831), generates a group key,
and enables Private positions. Friends select Join group and enter the host's
address, matching port and key. Share these details privately.

No account, VPN application, public IP lookup service, STUN server or external
relay is required. The hosting player's app forwards authenticated positions
between up to eight joining clients. Every participant needs MiniMap running.

Host mode asks the local default gateway for its public IPv4 using NAT-PMP, with read-only UPnP WAN discovery as a fallback. No outside IP lookup is used.
Enabling hosting also requests a temporary TCP mapping on that gateway. The
app renews the lease while hosting and requests deletion when sharing stops.
The app requests a ten-minute lease; after a crash the router lets its finite lease expire.
Detection alone only reads the gateway address; it does not open a port.

Automatic router forwarding supports NAT-PMP. UPnP-only routers can still supply the public address, but need manual port forwarding. Router discovery must be enabled in the router; if NAT-PMP and UPnP are both unavailable, enter the WAN address manually.
Hosting automatically adds or reuses a persistent Windows Firewall rule for the exact executable path and selected inbound TCP port, across Private, Public and Domain profiles. The app reads the rule back before reporting success. Joining does not create an inbound exception. The rule can be removed in Windows Firewall; deliberately disabled or unrelated rules are not overridden. If creation or verification fails, the app reports that separately and you can allow this app and port manually. A router mapping does not prove external reachability.
If automatic setup is unavailable, forward the selected TCP port on your router
to this PC's LAN IPv4, allow it through the firewall and enter your public IPv4.
You can find the WAN address in your router's administration page. A router
behind another router may need forwarding at both. Under carrier-grade NAT,
ask your ISP for a public IPv4; peer-to-peer cannot bypass that by itself.

For PCs on the same LAN, use the hosting PC's LAN address from the named adapter
list. Router forwarding is unnecessary. Avoid WSL/Hyper-V addresses when sharing
across the internet. A failed automatic setup does not prevent LAN connections.
If the router assigns a different external port, MiniMap withdraws that mapping
and asks for manual setup rather than advertising an incorrect port.

The address field stays editable until sharing starts. A delayed lookup does not
overwrite a manually entered address or a Join destination. No credentials are
saved; sharing starts disabled on each launch. Closing Settings keeps sharing
active; close Settings on both PCs to resume normal game position capture.
Switch sharing off and on to reconnect after failure. Both PCs must use this updated build (sharing protocol 3). Different PC clock settings are supported.
A listening host with zero authenticated connections is waiting for a friend.

Names and coordinates use AES-256-CBC with HMAC-SHA256 and separately derived
keys. Each packet has a fresh IV. Network observers can still see endpoints,
packet sizes and timing. Everyone with the group key is trusted and can provide
false names or positions. Generate a new key when membership changes and clear
the clipboard after sharing it. The app cannot verify the participants' SCUM server.

Player markers display names without last-seen timestamps. Friends outside the minimap view have a cyan direction arrow and name at the edge. Updates run once per second using already-captured positions. Capture age is carried internally instead of comparing clocks between PCs; relaying preserves that age and sequence numbers reject replayed updates. Pausing capture
withdraws the local publication. Positions expire after 30 seconds without a
fresh capture. Disconnect clears the local peer view; losing the host clears
the client session. No game memory, admin access or non-participant locations
are used.

Settings > Appearance > Rotate minimap with heading keeps the player facing up
on the compact HUD; the tactical map stays north-up. POI icon sizes are bounded,
nearby locations cluster into count badges, and zooming separates them. Hover
clusters to see names. World anchors and label spacing keep both maps readable.





## Shared waypoints and death markers

Enabling Private positions also shares up to 128 saved point waypoints and death
markers from the current map. Recent death markers are prioritised. Custom area
outlines, built-in POIs and received markers are not exported as your own.

Friends see cyan waypoint diamonds and orange death tombstones, labelled with
the owner's name. Shared death labels have no elapsed-time text. Hover gives
marker details; click a shared marker or use Set waypoint to navigate to it.
Received markers are read-only and are never added to your saved waypoint files.
You can explicitly save a separate copy using the normal Save waypoint action.

Adding, renaming, moving or deleting one of your own markers updates the group's
snapshot. Connected members can still share saved markers while live position
capture is paused. Turning sharing off clears your received markers immediately;
a disconnected member's cached markers expire within 30 seconds. Closing the app
ends sharing. Your own saved markers remain in your profile.

Settings > Appearance > Cluster nearby POIs turns clustering on or off for both maps. Off shows individual category icons; on groups nearby locations into count badges. The preference is saved and defaults to on.

The active GPS navigation destination is shared automatically even if it has not been saved. Selecting a different destination updates its pin; clearing GPS removes that active-destination pin. The active GPS uses one slot of the 128-marker limit and is prioritised over saved markers. Off-screen shared GPS destinations have a cyan minimap edge arrow and owner label. If the same location is also a saved waypoint, that saved pin remains when GPS is cleared.

The big-map sidebar lists connected players, including players without current coordinates. Each player has separate visibility pills for live location, active GPS waypoint, saved locations and death markers. These session-only controls apply to both maps and do not change outgoing sharing. All four start enabled; players with the same name retain separate controls. Connection status contains no last-seen ages.
