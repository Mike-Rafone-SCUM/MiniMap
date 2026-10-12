# Queued coordinate input and chat — v1.7.22

The sender previously checked whether chat was paused before calling SendInput. That API accepts an input request into Windows' queue; it does not mean SCUM has received the key yet. The low-level hook then passed all injected events through without checking whether the copy request was still valid. A pending Num / key-down could therefore pass after chat opened and cancellation had ended its original copy operation.

Coordinate copies now have a unique per-request tag and a delivery gate. The low-level hook rechecks the original request's chat, focus, tracking and input context before forwarding tagged key-downs. Cancellation permanently invalidates that request. Missing or expired requests are blocked, and an old tag cannot borrow a newer request's permission.

Tagged key-ups remain allowed so cleanup can release an already accepted copy key or modifier. Admin-command input uses a separate tag and keeps its existing ownership checks. Physical keys and other applications' injected input are outside the coordinate-copy filter. Do not collapse these input tags back into one shared value.

The regression invokes the actual keyboard-hook callback with a queued copy packet. It failed on the previous hook and passes after the change. Further checks cover default/configured chat keys, reopening/closing chat, newer requests, focus loss, cleanup releases, admin input, physical input and an unavailable context.

Verification:


This verifies the queued-input failure path. A live SCUM retest is still required to confirm the reporter's symptom is fully resolved. If Num / continues to appear while chat has already been open for some time, collect the bounded shortcut trace to check whether the chat-opening event was recognized or the chat gate was cleared. This change does not claim to identify every possible missed-hook or game-UI state issue.
