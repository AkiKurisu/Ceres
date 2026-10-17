# Event Dispatch

| Field | Value |
|---|---|
| Version | 1.1.2 |
| Status | Living |
| Date | 2026-10-18 |
| Related Specs | [Flow Runtime Execution](../Flow/runtime-execution.md) |

This specification owns the event dispatch contract: who owns an event object,
when a sent event is delivered, in which order, how events merge, how they
propagate and how subscriptions end. Usage belongs to the
[Events guide](../../Documentations/docs/core_events.md) and the
[Reactive Integration guide](../../Documentations/docs/core_reactive.md). Where
a guide summarizes delivery timing, this specification is authoritative.

## Purpose and Boundary

Ceres Events delivers typed, pooled event objects to callbacks registered on
event handlers. Ceres owns the event object model, coordinators, dispatchers,
dispatch strategies, propagation, the observable bridge and the Editor debugger.
The product owns its event types and their payloads, which coordinators exist,
which frame phase each sender targets, which dispatchers merge events and when a
merge window closes.

Terms used below:

- An **event handler** is the object callbacks register on and events target.
  Handlers may form a parent chain.
- A **coordinator** is a scene object that ticks with the frame and owns one
  dispatcher. Every handler belongs to exactly one coordinator. The global
  coordinator serves application-wide messages.
- A **dispatcher** processes the events of its coordinator through an ordered
  strategy chain.
- A **frame phase** is one of update, fixed update and late update.

## Event Objects and Ownership

Events come from a per-type pool and are reference counted. Taking an event from
the pool gives the caller one reference. An event returns to its pool, reset,
when its last reference is released. Reusing an event whose references were not
balanced is reported.

Every holder owns its own reference:

- **Sender**: owns the reference it took from the pool and releases it after
  sending, normally in the same scope. Releasing immediately after a send is
  always safe, whether or not the event was delivered yet.
- **Queues**: a coordinator phase queue or a dispatcher wait queue acquires a
  reference when the event enters and releases it after delivery.
- **Subscribers**: a callback borrows the event for the duration of the call and
  holds no reference. An observable subscription acquires one reference per
  delivery, which the subscriber releases.
- **Merging**: a merged event is owned by its batch listener until it is
  flushed.

An event instance is dispatched at most once; a new delivery needs a new event.
No holder keeps an event after its delivery or release. Values that must outlive
a callback are copied out of the event.

References stay balanced on every path, so every event, including a merged
event, returns to its pool after its last delivery.

## Coordinators and Frame Phases

A coordinator ticks in update, fixed update and late update and records the
phase it most recently entered. The record persists between ticks; before its
first tick a coordinator counts as being in update.

A send names a target phase, update unless stated otherwise, and a mode:

- **Default mode**: when the target phase is the coordinator's most recently
  entered phase, the event is dispatched synchronously within the send. Otherwise
  it is queued for the target phase and delivered at the coordinator's next tick
  of that phase.
- **Immediate mode**: the event is dispatched synchronously regardless of phase
  and nests inside any event the dispatcher is processing.

"Most recently entered" is not "currently inside". An update-targeted event sent
after the coordinator's update tick and before its late update tick is delivered
synchronously, which covers later update scripts, animation events and most
asynchronous continuations. Sent after the coordinator's late update tick, it
waits for the next frame's update; sent during fixed update, it waits for that
frame's update. The coordinator's order relative to other scripts in the same
phase is defined only when the product defines it, so a send from another script
in the same phase may be synchronous or may wait one tick. A sender that needs a
fixed answer uses immediate mode or a phase whose order it controls.

At each tick, the coordinator first delivers the events queued for that phase in
send order, then flushes merged events (see Batching). Events still queued when a
coordinator is destroyed are not delivered.

The global coordinator is created on first access in Play Mode, survives scene
loads and lasts for the process. In the Editor outside Play Mode there is no
global coordinator.

## Dispatcher and Re-entrancy

A dispatcher processes one event at a time through all of its phases, including
default actions. An event sent in default mode while the dispatcher is
processing another event waits in the dispatcher. It is processed right after
the event in progress finishes, before events that were already waiting, and
events sent from one callback are processed in send order. Each event's
consequences therefore complete before the next waiting event starts, and no
event interleaves with one in progress. Immediate mode is the only way to process
an event inside another event's callbacks.

This rule is independent of the phase rule: a send that the phase rule delivers
synchronously still waits for an event in progress on the same dispatcher.

Callback registrations changed during a delivery take effect after the handler
finishes invoking its current callbacks.

## Strategy Chain

A dispatcher passes each event through an ordered strategy chain that is fixed
when the dispatcher is created. A strategy may claim an event; a strategy that
stops dispatch or propagation ends the chain. The default strategy propagates an
event whose target belongs to the dispatching coordinator and ends every chain
that reaches it, so specialized strategies such as batching come before it. In
the Editor the debugger strategy is always first.

## Batching

Batching is opt-in per dispatcher: its chain contains the batching strategy and
the dispatcher has a batch listener. Only event types that declare themselves
mergeable are batched.

- **Key**: the event type and the dispatcher. Merging never crosses types or
  dispatchers.
- **Acceptance**: an accepted event is stopped. It has no propagation and no
  default action; subscribers see only the merged event.
- **Merge**: the first event of a key since the last flush creates a new pooled
  merged event that carries the first event's payload. Each later event of that
  key merges its payload into the merged event. A mergeable type defines both
  steps so that no sent payload is lost.
- **Flush**: at the end of every coordinator tick in any phase, after that
  phase's queue is delivered, the listener dispatches each pending merged event
  once and releases its own reference. The merged event returns to its pool when
  that delivery and any subscriber references end. The order between keys within
  one flush is not defined.
- **No merging during a flush**: while a flush runs, events of a type being
  flushed, including the events its subscribers send, are dispatched directly
  instead of merged.
- **Deferred flush**: the listener is an extension point. A product listener may
  skip flushes to keep merging open across a window it defines, such as an update
  that spans several frames; pending merged events then flush at the first tick
  after the window closes. A product that defers flushes closes the window on
  every exit path, including failure.

The merge window is everything sent to the dispatcher since its last flush. It
can end within the same frame: a change sent after the coordinator's update tick
flushes at its late update tick.

## Propagation and Default Actions

An event targets one handler. An event that bubbles or trickles down builds its
path from the target's parent chain when it is dispatched; any other event
visits only its target. The phases run in this order:

1. **Trickle down**: ancestors from the root toward the target run their
   trickle-down callbacks.
2. **At target**: the target runs its trickle-down callbacks, then its other
   callbacks.
3. **Default action at target**: the target runs its own at-target behavior.
4. **Bubble up**: ancestors from the target's parent toward the root run their
   bubble callbacks.
5. **Default action**: the target runs its final behavior. A composite ancestor
   on the path also runs its final behavior for the event.

Stopping propagation lets the remaining callbacks of the current handler run and
skips later handlers. Stopping immediate propagation also skips the remaining
callbacks of the current handler. Preventing the default skips both default
action phases.

Propagation never crosses coordinators: handlers that belong to a coordinator
other than the dispatching one are skipped. An event may ask to skip handlers
whose owning behaviour is inactive or disabled; callbacks registered to include
disabled handlers still run.

## Subscription and Lifetime

- **Direct callbacks**: the owner registers a callback on a handler for one
  phase and unregisters the same callback when its lifetime ends. Registering
  the same callback twice for one phase has no effect.
- **Observable bridge**: a handler's events can be consumed as an observable
  stream. When the handler is bound to a behaviour, the stream completes and its
  callback is unregistered when that behaviour is destroyed; otherwise the
  subscriber ends the subscription.
- **Safe subscription**: releases each delivery's reference after the callback
  returns and is the normal way to consume events through the bridge. A plain
  subscription owns each delivered reference and must release it exactly once.
  Callbacks do not let exceptions escape, because an escaping exception skips the
  release.
- **Flow entry points**: [Flow](../Flow/runtime-execution.md) programs subscribe
  their event entry points through safe subscriptions that end with the runtime
  owner.

Events are not sticky. A subscriber registered after a delivery does not receive
that event; a consumer that needs the current state reads it before subscribing.

## Debugger

The Event Debugger exists only in the Editor; players contain no debugger
strategy and no recording. In the Editor an attached debugger observes
dispatches, callbacks, propagation paths and default actions, and can record,
save, load and replay selected events. Observation never changes dispatch order,
timing or reference counts, and replaying a recorded event sends a new event.
Recordings are diagnostics, not a runtime persistence mechanism.

## Validation

Validate a change to these flows with tests that cover:

- synchronous delivery when the target phase is the most recently entered one,
  and queuing to the next tick of the target phase otherwise, for sends after
  each coordinator tick and during fixed update;
- immediate mode nesting inside a callback;
- re-entrant sends processed after the event in progress and before earlier
  waiting events, in send order;
- one merged delivery per type and dispatcher per flush, flushes at the end of
  every phase tick, direct delivery of same-type events sent during a flush, and
  a deferred flush delivered once after its window closes;
- propagation phase order, stop and prevent-default semantics, skipped handlers
  of other coordinators and of disabled behaviours;
- balanced references after send and release, queued delivery, safe and plain
  subscriptions and merged events, checked by every event returning to its pool;
- unchanged dispatch order and reference counts with the debugger attached.

Inspect live delivery with the Event Debugger in Play Mode.
