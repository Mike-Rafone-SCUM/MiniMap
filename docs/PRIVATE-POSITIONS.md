# Private positions

## Persistent private groups (default)

Open Settings > General > Private positions. A display name is filled in for you;
you can change it. Click **Create group and copy code** to create and connect to
your group. Give that code privately to your friends. They paste it into **Group
code** and click **Connect**. Nobody enters an IP, port or connection mode, and
nobody needs home port forwarding, a VPN account or a public home address.
All members need the persistent-group build. Close Settings to resume normal
game position capture.

Codes stay valid until their owner clicks **Refresh code** or **Delete group**. A refresh disconnects
that group's existing members and makes the old code unusable. Share the new code
to admit people again. Only the creator has the separate owner credential; it is
never included in the shared group code. Keep the creator's saved Windows profile
to retain ownership. The current application stores codes and owner credentials
encrypted with Windows DPAPI in `private-groups.dat` inside its profile directory.
They are tied to that Windows account, not a portable ownership backup.

You can save up to 16 groups and connect to several at once. Each saved group has
its own Connect/Leave and Copy code controls; owned groups also show Refresh code and Delete group. Deletion requires confirmation, disconnects all members and permanently retires the code. Failed deletion remains saved for retry. Leave disconnects your session, disables automatic rejoining and keeps the saved code.
Closing MiniMap keeps your active memberships saved. Opening it again automatically reconnects those groups, including when their creator is offline. Explicit Leave keeps that group disconnected after restarting. Older membership files retain their previous disabled startup preference until the next Connect. Rooms with no connected members
are removed from memory immediately; their persistent codes and ownership remain
valid. There is no seven-day inactivity expiry. The server stores opaque group/routing and membership identifiers, owner-credential hashes and encrypted member labels, never decryption keys or position history. Member names remain encrypted. Offline members stay in the roster across empty rooms and relay restarts. They have no live position or shared map markers. Up to 64 memberships can be remembered per group. Each group shares only your own positions and markers; joining
several groups does not forward one group's members into another group.

The configured Oracle relay uses outbound TLS connections and a pinned server
certificate. Names, positions, GPS destinations, waypoints and death markers are
encrypted and authenticated on the participants' PCs. The relay receives an
opaque room identifier derived from the key, never the key itself. It forwards
encrypted packets without decoding them or retaining location history. The relay
operator can still see connecting IP addresses, room membership, sizes and timing.
People with your group code can read and publish in your group. Refresh the code
when membership changes. Creating or connecting to a group enables automatic rejoining for that group; Leave disables it.

Each room supports nine participants. The service has connection/rate limits and
a conservative 50 GB monthly traffic budget with an automatic cutoff. Unused
reserved traffic is forfeited on restart; restarting does not reset the budget.
The allowance renews at the start of each UTC calendar month. Dropped network connections retry after 30 seconds. Host Disconnect stops automatic retries for the current app session; the member may still use Connect or restart MiniMap to rejoin. The direct-IP Host/Join button has been removed. No paid fallback is used.

The relay's budget limits this service only. It cannot guarantee a zero Oracle
bill or account-wide availability: other workloads share the account's free
allowances, and Oracle controls infrastructure availability and billing.

## Connection and capture

Group codes through our Oracle relay are the connection flow. There is no direct-IP Host/Join button. Close Settings and focus SCUM to resume coordinate capture. Previously active groups reconnect on startup. Connect enables this for a saved group, and Leave disables it. App and relay updates are both required for persistent offline membership.

Names and coordinates use AES-256-CBC with HMAC-SHA256 and separately derived keys. Each packet has a fresh IV. Everyone with the group code is trusted and can publish names and positions; the app cannot verify their SCUM server. Keep codes private. The creator alone holds the separate permission to refresh or delete a group. The relay keeps a deleted-group tombstone containing only existing routing/owner hashes, preventing old codes or creation requests from reviving it.

Capture age is preserved across clients with different PC clocks. Positions expire after 30 seconds without a fresh capture. Disconnect removes received live locations; teammate cards show connection state and time since the captured location. Friends use the same heading-arrow shape as your own marker, and off-screen friends have direction indicators. No game memory, admin access or non-participant locations are used.

## Group names and squad routes

Give a group a name when creating it, or edit its name and Save group name. Any member can update the encrypted name; it is saved with the Windows-protected membership without changing the code. New members learn the name when another member is online.

Right-click the map for squad tools, route stops and teammate navigation. Context menus retain the original press location when the mouse drifts. Every menu level is drawn inside the existing map window without native popup activation or foreground focus switches. Escape closes the menu first; clicking outside or hiding the map dismisses it. Floating information cards accept map right-clicks too.

Up to 16 route stops calculate automatically as they change. Solo routes use amber; shared squad routes use cyan. Connected groups automatically see personal plans. Following a shared route applies its owner's edits without publishing a duplicate personal plan or forwarding it into other groups. Calculations happen locally; road geometry is not sent to the relay. Dashed legs are direct guidance without a verified road route. Start route begins navigation and advances within 40 metres. Follow teammate updates a moving target and pauses without a fresh position.

Danger, Loot and Pick me up pings expire after 60 seconds; shortcuts start unbound and a two-second cooldown limits repeated pings. Map presets save local view/filter settings for the matching map.

## Shared waypoints and death markers

Connected groups share a bounded snapshot of up to 128 point markers, including active GPS, saved waypoints, death markers, pings, route stops and group-name metadata from the current map. Recent death markers are prioritised. Custom area
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

The big-map sidebar lists connected and remembered offline members, including players without current coordinates. Each player has separate visibility pills for live location, active GPS waypoint, saved locations death markers and routes. These session-only controls apply to both maps and do not change outgoing sharing. All five start enabled; players with the same name retain separate controls. Connection cards show status and the age of the last captured position.
