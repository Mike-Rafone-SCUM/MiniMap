These private regression fixtures are the operator-supplied SCUM clipboard reports from 2026-10-08: all squads, spawned vehicles, current players and flag page 1/2. They contain real platform IDs and names and must not be included in public release assets or public source commits.

AdminPlayerTests additionally constructs a representative page 2/2 using two rows from the supplied second-page example to exercise pagination, duplicate detection and atomic replacement. The test does not assert the live server's complete flag count.
