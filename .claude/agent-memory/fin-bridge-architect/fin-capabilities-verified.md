---
name: fin-capabilities-verified
description: Verified FicsIt-Networks Lua API facts the bridge depends on (InternetCard futures, event.pull, findComponent) with confidence tags
metadata:
  type: reference
---

FIN Lua capabilities verified against docs.ficsit.app and the Panakotta00/FicsIt-Networks
GitHub source (docs adoc files). WebFetch is 403-blocked on docs.ficsit.app; use the
raw.githubusercontent.com adoc paths or WebSearch instead.

VERIFIED:
- InternetCard:request(url, method, body, ...headerPairs) returns a Future.
  req:await() yields (statusCode:number, body:string). Headers are flat trailing string
  pairs.
- Futures: await(), poll(), get(), canGet(); future.join(...) runs several in parallel;
  future.run() services scheduled futures inside a loop.
- event.pull(timeout): no arg blocks forever; >0 blocks up to N seconds then returns nil;
  0 returns immediately (non-blocking, same tick). Returns tuple (eventName, sender,
  ...args).
- event.listen(...), event.ignore(...), event.clear(), event.filter{event=,sender=,values=}.
- component.findComponent(query|class) -> string[] UUIDs (nick/group query supported);
  component.proxy(id|ids) -> Object|Object[].
- Canonical cooperative loop: `while true do local e = event.pull(0); future.run(); if not
  e then computer.skip() end end`. This confirms one loop can BOTH service the long-poll
  HTTP future AND drain the signal queue.

PROBABLE: computer.millis()/computer.time() for agent clock (used only as advisory
agentTimestamp; server stamps authoritative receivedAt). EEPROM/script storage limited →
keep agent small, heavy logic server-side.

SPECULATIVE: exact InternetCard in-flight concurrency. Protocol assumes one long-poll +
short result/event POSTs per agent. Fallback if single-in-flight: agent serialises a
separate short poll between holds (same envelopes, one extra round trip, NO wire change).

See [[protocol-v1-contract]].
