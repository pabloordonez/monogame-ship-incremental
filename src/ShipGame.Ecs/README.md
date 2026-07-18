# ShipGame.Ecs

This is a small sparse-set ECS used by Simulation. It depends only on Domain. There are few files on purpose. Entity identity, typed stores, the world, the structural command buffer, and the explicit system scheduler all live at the project root.

```mermaid
flowchart LR
  Create[World.Create] --> Set[World.Set components]
  Set --> Schedule[SystemScheduler.Tick]
  Schedule --> Buffer[CommandBuffer enqueue]
  Buffer --> Apply[Apply at sync point]
  Apply --> Create
```

## Rules that keep runs deterministic

Components are data. They do not load assets, call services, or own RNG instances. Structural changes (create, destroy, add, remove) must not happen while a query is iterating. Enqueue them on `CommandBuffer` and apply at a known synchronization point.

The scheduler is an ordered list you register by hand. There is no attribute scan and no reflection auto-ordering. When Simulation adds a system, it appends it in the exact tick order the design requires.

## Entity identity

`EntityId` pairs an index with a generation. Reused indexes bump generation so stale handles fail instead of silently pointing at a new inhabitant. Prefer checking aliveness through the world rather than caching raw indexes.

## When to extend this project

Add store or scheduler capability here only when Simulation truly needs a new primitive. Most gameplay growth belongs in Simulation systems and components, not in a thicker engine layer.
