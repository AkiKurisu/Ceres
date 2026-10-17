# Game World

| Field | Value |
|---|---|
| Version | 1.1.2 |
| Status | Living |
| Date | 2026-10-18 |

This specification owns the Gameplay world contract: how a world and its
subsystems are created, initialized and released, when and in which order
subsystems tick, and how the world may be accessed while it is destroyed. Usage
belongs to the [World and Actors guide](../../Documentations/docs/gameplay_world.md);
scene transitions are described by the [Level guide](../../Documentations/docs/gameplay_level.md).

## Purpose and Boundary

The world is the scene-level ownership boundary of Gameplay. It tracks actors
and owns world subsystems, services whose lifetime is the world's lifetime, so
that world state does not become process-wide singletons. At most one world is
current at a time.

Ceres owns the world and subsystem lifecycles, the tick phases, subsystem order
and actor handles. Products own their subsystems, their order values and the
work each subsystem does in each phase.

## World Lifecycle

**Creation.** A world placed in a scene becomes current when it awakes; another
world that awakes while one is current is destroyed. When no world exists and
access is allowed, accessing the world creates one.

**Construction.** A world, when constructed, creates every subsystem declared to
be created with the world. Only runtime code contributes such subsystems;
Editor-only code never does. Each subsystem is asked whether it can exist in this
world, and one that declines is not created.

**Initialization.** Subsystems created with the world initialize when the world
starts, before its first tick. A project setting can make a lookup initialize a
subsystem that has not initialized yet, so that a lookup made before the world
starts never returns an uninitialized subsystem. Each subsystem initializes
exactly once.

**Release.** When the world is destroyed, every subsystem it owns is released
exactly once and loses its world. Contexts that retained the world become
invalid, and lookups through them return nothing.

## Subsystems Created on Demand

A subsystem that is not created with the world is created on its first request
while the world is valid, if the subsystem accepts the world. Registration
initializes it immediately, and lookups find it at once. Registration into a
world that is being destroyed is not allowed.

The world ticks a snapshot of its subsystems and rebuilds that snapshot when a
phase ends. A subsystem registered while the world is running joins the snapshot
at the end of the first phase the world finishes after the registration, and
first ticks in the phase after that. It never ticks in the phase during which it
was registered.

## Tick Phases

The world ticks its subsystems in three phases:

- **Tick** runs in the frame's script update, ahead of scripts with ordinary
  execution order.
- **FixedTick** runs in every fixed-step update, with the same precedence.
- **LateTick** runs after every MonoBehaviour LateUpdate of the frame and before
  the Player Loop systems that follow them, such as animation, skinning and
  rendering preparation. It is the place for work that must see the final
  results of all late update scripts and must finish before later systems read
  them.

Only the current world ticks. Tick and FixedTick follow the world's enabled
state. LateTick does not run while the world has not started, is disabled or
inactive, or is destroyed, and it runs only while the application is playing. An
exception thrown during LateTick is reported and ends that phase for the frame
without stopping the Player Loop.

## Order

Within each phase, subsystems run by their declared order value, lowest first.
A subsystem that declares no order has order 0, and subsystems with equal orders
keep their registration order. The same order applies to all three phases.

Subsystems created with the world are registered together during construction,
in an order the product does not control. Subsystems that need a fixed relative
order therefore declare different order values.

## World Access During Destruction

In the frame a world is destroyed, world access is refused: the world reports
itself invalid, lookups return nothing and access does not create a new world.
Teardown code in objects destroyed in the same frame therefore cannot resurrect a
world. From the next frame on, access may create a new world.

A level transition that replaces the scene keeps world access allowed while the
replacement scene loads, because objects of the incoming scene awake in the same
frame as the outgoing world is destroyed. Access during that transition may
create the world of the incoming scene. Code that retains a world context checks
its validity before every use.

## Actors and Handles

Actors register with the current world when they awake and unregister when they
are destroyed. Registration yields a versioned handle; once an actor leaves the
world, its handle no longer resolves, even after its slot is reused.

## Validation

Validate a change to these flows with Play Mode tests that cover:

- LateTick observing the results of every late update script and finishing
  before the Player Loop systems that follow them;
- no LateTick before the world starts, while it is disabled or inactive, or
  after it is destroyed;
- ascending order values with a default of 0, registration order for equal
  values and the same order in every phase;
- on-demand subsystems that initialize at registration, resolve immediately,
  skip the phase in which they were registered and tick from the following
  phase;
- single initialization and single release of every owned subsystem, including
  subsystems registered but not yet ticking;
- duplicate worlds destroyed, refused access and no recreation in the
  destruction frame, recreation from the next frame, and access during a level
  transition;
- handles that stop resolving after their actor leaves the world.
