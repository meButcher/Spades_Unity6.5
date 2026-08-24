# Spades

A complete, playable game of Spades in Unity 6.5, in both four-player partnership and two-player
head-to-head modes, with the full standard ruleset and animated presentation.

The whole ruleset is a plain C# library that has never heard of Unity. The Unity project is a
renderer that subscribes to what that library says happened.

---

## How to run

**Requires Unity 6000.5.7f1** (Unity 6.5) or newer in the 6.5 line. No external packages, no Asset
Store imports, no art downloads.

1. Clone and open the project in Unity.
2. Open `Assets/Scenes/Main.unity`.
   *If it does not exist yet, use the menu item **Spades → Create Main Scene**. The editor script
   builds it, and it also runs automatically the first time the project is opened.*
3. Press **Play**, choose 4 Player or 2 Player, and play a full game to 500.

### Running the tests

**Window → General → Test Runner → EditMode → Run All.**

63 tests, about two seconds. They include two five-hundred-seed full-game simulations, which is
where most of the value is — see [Testing](#testing).

---

## Architecture

```
┌──────────────────────────────────────────────────────────────┐
│  Spades.Unity        MonoBehaviours, tweens, procedural art  │
│                      Presenter, Views, Panels, Bootstrap     │
└───────────────▲───────────────────────────┬──────────────────┘
                │ events                    │ commands
┌───────────────┴───────────────────────────▼──────────────────┐
│  Spades.Core         Cards · Rules · State machine ·         │
│  (No Engine Refs)    Scoring · AI · Commands · Events        │
└──────────────────────────────────────────────────────────────┘
                        ▲
                        │ referenced by
              ┌─────────┴──────────┐
              │  Tests (EditMode)  │  NUnit
              └────────────────────┘
```

Four assembly definitions:

| asmdef | References | Setting |
|---|---|---|
| `Spades.Core` | none | **No Engine References ✔** |
| `Spades.Unity` | `Spades.Core`, `UnityEngine.UI`, `Unity.TextMeshPro`, `Unity.InputSystem` | normal |
| `Spades.Editor` | `Spades.Core`, `Spades.Unity`, and the same three packages | Editor only |
| `Tests` | `Spades.Core` | Editor only, NUnit |

Note that the package assemblies are listed explicitly. `autoReferenced` on a package only makes it
visible to the *predefined* assemblies (`Assembly-CSharp`); an assembly definition sees nothing it
has not declared. That is the same trade the whole layering rests on — you state your dependencies
or you do not get them.

**Lead with the first row.** Ticking *No Engine References* on `Spades.Core` makes the separation
**compiler-enforced rather than a convention**. The day someone types `Debug.Log` inside the
scoring code, the build breaks. Every reviewer has seen a project where the logic layer slowly grew
`Transform` references; an asmdef is how you make that impossible instead of merely discouraged.

Second consequence, and the one you feel daily: `Spades.Core` compiles and its tests run without
entering Play mode. The rules suite runs in about a second, which is why it is affordable to have
one that covers everything.

```bash
grep -r "UnityEngine" Assets/_Project/Core
```

Returns nothing.

### Commands in, events out

```
   [ Human clicks a card ]            [ AI decides ]
              │                             │
              └──────────┬──────────────────┘
                         ▼
                  IGameCommand              PlayCardCommand(seat, card)
                         ▼
              ┌────────────────────┐
              │   GameLoop         │  validates with LegalMoveValidator,
              │   (state machine)  │  mutates GameState, emits events
              └────────┬───────────┘
                       ▼
                  IGameEvent[]              CardPlayed, TrickWon, HandScored…
                       ▼
              ┌────────────────────┐
              │  GameEventQueue    │  drained one at a time by GamePresenter
              └────────┬───────────┘
                       ▼
              tween sequences → views update → next event
```

Why this rather than the view calling methods on a `GameManager`:

1. The core holds **no reference to the view**, not even an interface it calls synchronously. It
   emits a value object and forgets.
2. The event stream is a **log**. Tests assert on it directly (`GameHarness.ValidateEventStream`
   checks every hand of every simulated game from the events alone), it can be replayed, and — the
   multiplayer point — it can be serialised down a wire without changing the core.
3. Commands are the only way in, so **validation lives in exactly one place**. There is no path
   into `GameState` that skips the rules.

### The hard problem: where the animation time lives

The core resolves a trick in microseconds. The player needs most of a second of cards flying to the
winner to understand what happened. Where does that time live?

Three answers, two of them wrong:

- **Put a wait inside the game logic.** Now the rules depend on `UnityEngine.Time`, cannot be unit
  tested, and run at the speed of the animation. This is precisely what the asmdef prevents.
- **Let the core run to completion and have the view catch up.** The core finishes the hand
  instantly while the view is still on trick three. Two sources of truth about what is happening,
  and every input has to be validated against a state the player cannot see.
- **✔ The core is a step machine that only advances when driven, and the view drives it.**

`GameLoop` exposes `Advance()`, `IsAwaitingInput` and `AwaitingSeat`. It never blocks, never yields,
and the string `Time` does not appear in `Spades.Core`.

`GamePresenter` runs one coroutine:

```
loop:
    GameLoop.Advance()
    drain the event queue:
        take the next IGameEvent
        play its animation, wait for every tween to finish
    if the loop is parked on the human seat:
        unlock input, wait for a click, submit the command
    else if a bot already answered:
        hold a beat so the move is legible
```

Two properties fall out of that ordering:

- All timing lives in the view. A bot answers instantly; the *pause* is added by the presenter,
  which is why five hundred simulated games run in a second while a played game feels paced.
- Input is unlocked at **exactly one place** — queue empty *and* core parked on a human seat. Click
  during an animation and double-submit bugs are structurally impossible, not patched: while an
  animation runs, the code that could accept a click has not been reached.

### The re-entrancy trap, and the rule that avoids it

`AiPlayerController` calls `submit` immediately and synchronously, from inside `Advance()`. If
`submit` executed the command, one call to `Advance()` would recurse through an entire hand before
the view drew a frame — silently landing you back at the second wrong answer above.

> **`submit` never executes. It parks the command in a single-slot mailbox, and `Advance()`
> processes at most one decision per call, then returns.**

`GameLoop.Advance()` asserts that it never emits more than one `CardPlayed`, so this cannot regress
quietly, and `GameLoopTests.Advance_NeverPlaysMoreThanOneCardPerCall` checks it from the outside.

### Hidden information

Neither the AI nor the UI ever receives `GameState`. They receive `SeatView`, a projection for one
seat containing that seat's hand and otherwise only public information (bids, trick counts, hand
*sizes*, cards already played face up, scores).

This makes an AI that cheats **structurally impossible** — the heuristic cannot peek at your hand
because it was never handed it. It is also exactly the hidden-information boundary a card-game
server needs, and it costs nothing to build this way from the start. `SeatView` is a `readonly
struct` over the live backing arrays, so projecting one allocates nothing.

The view honours the same boundary: an opponent's `HandView` holds face-down placeholders that are
only bound to a real card at the instant it is played.

### The player seam

```csharp
public interface IPlayerController {
    void RequestBid(SeatView view, Action<int> submit);
    void RequestCard(SeatView view, IReadOnlyList<Card> legalMoves, Action<Card> submit);
    void RequestDrawDecision(SeatView view, Card drawn, Action<bool> submit);  // 2P only
}
```

Two implementations ship: `AiPlayerController` and `HumanPlayerController`. Two-player versus
four-player is a different-length array of these and nothing else — seat count and team layout are
data, not `if` statements.

### Composition root

`GameBootstrap.StartGame(playerCount)` is the only place concrete types are constructed. No
singletons, no `FindObjectOfType`, no DI framework: the object graph is a dozen lines, and a
framework would hide it rather than explain it. What the explicit graph buys is that `GameLoop` is
constructible in a unit test with no scene at all, and that swapping the human seat for a bot — or
later for a networked controller — is a one-line change in one file.

---

## Rules implemented

Full standard Spades. Three edge cases are where implementations quietly go wrong, and all three
are implemented and tested by name.

**Table and deal.** Four-player: fixed partnerships, seats 0/2 against 1/3, seated alternately, 13
cards each. Two-player: hands are *drawn*, not dealt. Dealer rotates clockwise each hand; play is
clockwise; spades is always trump.

**Bidding.** Each player bids 0–13 individually starting left of the dealer. Bids need not escalate
and may total more or less than 13. In four-player the partners' bids sum to the team contract. A
bid of 0 is Nil: a ±100 side bet scored independently.

**Play.**
- ⚠ **You may not lead a spade until spades are broken** — until a spade has been played on a lead
  of another suit. **Exception: a player whose hand contains only spades may lead one.** Miss that
  exception and a legitimate hand has zero legal moves and the game deadlocks with no error.
- Follow the led suit if you hold it. If void, anything is legal, including a spade — and playing
  one is what breaks spades.
- Highest spade wins; with no spade, the highest card of the led suit.

**Scoring.**
- Contract made: `+10 × bid`, `+1` per overtrick, and those overtricks are bags.
- Contract set: `−10 × bid`.
- ⚠ **Bags accumulate across hands.** At 10 bags, apply `−100` and **subtract 10 from the counter,
  keeping the remainder.** A team on 9 bags that takes 3 overtricks scores the 3, hits 12, takes
  `−100`, and carries **2** into the next hand — not 0.
  *The penalty is applied in a loop rather than once: a team on 9 bags whose partners both bid Nil
  and who then take all 13 tricks lands on 22 bags, and a single subtraction would leave them still
  above the threshold with the second penalty never applied.*
- Nil made `+100`, Nil failed `−100`.
- ⚠ **A failed Nil's tricks count as bags for the team and do not help the partner's contract.**
  This is the rule people forget, and it is why `ScoreCalculator.ScoreTeam` takes the trick counts
  in three separate parts:

  ```csharp
  ScoreTeam(teamBid, contractTricks, failedNilTricks, currentBags, rules)
  ```

  A Nil bid contributes nothing to `teamBid`; the Nil bidder's tricks never reach `contractTricks`;
  if the Nil fails they land in `failedNilTricks` and become bags. Partner bid 5 and took 3, Nil
  bidder took 2 and failed: five tricks are on the table and the contract is still **set**.

- First team to 500 wins. If both cross in the same hand the higher score takes it; an exact tie
  plays another hand.

**Two-player draw phase.** Shuffled 52-card stock face down. The non-dealer goes first, turns the
top card, and either keeps it or discards it face up and takes the next one **sight unseen**. Turn
passes; repeat until both hold 13. Then bid and play 13 tricks exactly as in the four-player game.

That phase is a whole game phase that exists in one mode and not the other, and adding it cost one
new `GamePhase`, one command, two events and one panel. **Nothing in the trick engine changed.**

---

## Testing

63 EditMode tests. The interesting ones:

| Suite | Covers |
|---|---|
| `DeckTests` | 52 distinct cards, shuffle is a permutation, same seed reproduces the identical order |
| `TrickResolverTests` | spade beats non-spade, highest spade, and an off-suit discard losing *even when it outranks the winning card* |
| `LegalMoveValidatorTests` | follow-suit, void, spade-breaking, **the all-spades lead exception**, and that `GetLegalMoves` agrees with `IsLegal` for every card |
| `ScoreCalculatorTests` | every scoring case including **bag rollover carrying the remainder** and **double rollover**, plus the failed-Nil interaction |
| `BiddingTests` | bid bounds across 500 random hands, and that a hand holding a high spade never bids Nil |
| `GameLoopTests` | phase order, dealer rotation, at most one card per `Advance`, the loop parking on a human, and rejection of illegal cards with a reason |
| `PresentationLoopTests` | full games driven through the presenter's exact control flow with a human seat, across 60 seeds in both modes |
| `FullGameSimulationTests` | **complete games across 500 seeds, in both modes** |

### The one that punches above its weight

`FullGameSimulationTests` plays five hundred complete games of Spades with no scene, no GameObject
and no renderer, and asserts from the event stream alone that every hand played exactly 13 tricks,
that every card is accounted for, that trick counts sum correctly, that bag counters never carry a
value at or above the threshold, and that exactly one team finishes above the target with the
highest score.

That single fact — a thousand full games in about a second, headless — demonstrates the entire
architecture in one sentence. It is also what catches the deal nobody thought of: the two classic
failures it finds are a seat holding only spades before they are broken (zero legal moves, so the
game hangs) and a controller that executes instead of enqueuing.

---

## Optimization notes

Specific rather than vague, because vague claims invite probing.

- **`readonly struct Card`** — 52 immutable value identities copied constantly in the AI's inner
  loops. No heap allocation, no GC pressure. `IEquatable<Card>` is implemented explicitly so that
  `List.Contains` does not fall back to the boxing comparer, which the validator would otherwise hit
  on every card of every hand. `GetHashCode` is a perfect hash: `suit << 4 | rank` gives 52 distinct
  values in 0–63 with no collisions.
- **Allocation-free projection** — `SeatView` is a struct over `GameState`'s live arrays, so handing
  a seat its view costs nothing. Bids, trick counts and hand sizes are mirrored into flat arrays for
  exactly this reason.
- **Cached submit delegates** — `GameLoop` builds its three `Action` callbacks once in the
  constructor. They capture only `this`, so roughly sixty decisions per hand cost zero delegate
  allocations.
- **`GetLegalMovesNonAlloc`** — the engine fills a reused buffer on the hot path; the allocating
  overload exists for the UI and for tests.
- **No LINQ in the play loop** — plain indexed `for` loops throughout `LegalMoveValidator`,
  `TrickResolver` and both strategies. LINQ allocates an enumerator per call and this code runs tens
  of thousands of times in the simulation suite.
- **In-place Fisher–Yates** — one array per game, provably uniform. Note `rng.Next(i + 1)`, not
  `rng.Next(i)`: the off-by-one version can never leave a card in place and is measurably biased
  while looking correct.
- **Card view pooling** — 52 `CardView` objects built once at boot, never instantiated or destroyed
  again. This removes GC spikes mid-animation and, just as importantly, removes the classic
  `MissingReferenceException` fired from a tween completion against a destroyed transform.
  `CardView.Recycle()` kills the object's tweens *before* returning it to the pool, and `OnDisable`
  does the same as a backstop.
- **Tween object pooling** — `TweenRunner` recycles its internal tween records, so an animated hand
  does not allocate per frame or per card.
- **Procedural art** — the card faces and suit pips are generated at boot from their implicit
  equations into a handful of small textures. No atlas, no texture import, no per-card sprite asset.

---

## Multiplayer readiness

Deliberately scoped out for the time budget, but the architecture is shaped for it and here is
exactly how:

- `IPlayerController` is the seam. `RemotePlayerController` is one class: it forwards the request
  over the network and calls `submit` when the answer arrives. Nothing else changes.
- Commands are already value objects that carry a seat and a payload — they map directly onto a
  ServerRpc, and validation already lives server-side of that boundary in `GameLoop`.
- Events are already value objects the core emits and forgets — they map directly onto a ClientRpc.
- `SeatView` is already the per-client filter. The only event carrying private data is `HandDealt`,
  which a server would filter per client; every other event in `GameEvents.cs` is public information
  already.
- `GameEventQueue` is already the latency buffer. A view that consumes events at animation speed
  while the authority has moved on is the same problem as a view absorbing network jitter.
- `IRandomSource` is already injected, which is what a server-authoritative deal needs so every
  client agrees on the shuffle.

---

## Known limitations, and what I would do next

Things consciously not done, which is different from things not noticed.

- **Blind Nil is not implemented.** It needs a bidding sub-flow and a partner card exchange for
  about twenty seconds of reviewer attention. Cut on purpose.
- **The AI is heuristic, not search.** `HeuristicBiddingStrategy` is a scoring function (so any bid
  it makes can be explained by reading off the terms) and `HeuristicCardStrategy` is a priority list.
  It plays plausibly, protects a partner's winning trick instead of overtaking it, ducks to avoid
  bags once the contract is made, and never plays an illegal card. The real answer is a determinised
  Monte-Carlo search — sample the unseen cards consistently with the bidding and the void
  information, play out each determinisation with the current heuristic as the rollout policy, and
  pick the card with the best average. It is a day's work and a prototype does not need it. The
  interfaces (`IBiddingStrategy`, `ICardStrategy`) are the seam where it would drop in.
- **Animation is a small purpose-built tween system, not DOTween.** DOTween is an Asset Store import
  and this project has no external dependencies, which means the repo clones and plays with nothing
  to install. `TweenRunner` is about 180 lines: eased tweens with delays, a `WaitAll` barrier the
  presenter waits on instead of guessing durations, per-owner cancellation for pooling, and its own
  record pool. Swapping it for DOTween touches one file, because every animation in the project goes
  through that one type.
- **Card art is procedural**, generated from implicit equations rather than imported. It sidesteps a
  real failure mode as well as a licence: a font that happens not to contain U+2660 renders an
  imported-glyph deck as empty boxes, and no font is involved here. `CardView.Bind` is the single
  swap point for an imported deck.
- **The UI is built from code**, not from prefabs. The scene contains three objects and no wired
  references, which removes the whole class of failure where a reference is fine locally and null
  after a clone. The trade is that a designer cannot retune the layout by dragging; `LayoutSettings`
  exists so they can retune it by numbers instead.
- **One presenter with a switch**, rather than a class per event. At a dozen event types the switch
  is the more readable option and keeps the animation sequence for a whole hand on one screen. Past
  roughly twenty-five it should become a dictionary of handlers.
- **No persistence, no audio, no localisation.** Single resolution family tested (16:9, scaled).

### ScriptableObjects: used for three things, and deliberately not for a fourth

The rule: *a ScriptableObject holds authored data that is tuned in the editor and read at runtime.
It does not hold game state and it does not hold logic.*

`LayoutSettings` (sizes, durations, palette) and `GameRulesAsset` (player count, target score, bag
threshold, Nil bonus) qualify. `Rules_4P_200.asset` exists next to `Rules_4P_500.asset` to make the
point concrete: a rule variant is a duplicated asset, not a branch in the engine.

`GameRulesAsset` is **not** the `GameRules` the core uses. `Spades.Core` cannot so much as name
`ScriptableObject`, so the asset produces the plain object instead — the compiler enforces the
boundary rather than discipline.

There is a well-known talk advocating runtime *state* in ScriptableObjects. It is a legitimate
pattern and it is wrong here: asset state survives between play-mode entries (so you debug ghosts
from your last run), it cannot exist in an EditMode unit test, and it would put mutable game state
on the wrong side of the boundary the assembly definitions exist to enforce. `GameEventQueue` is
typed, testable and inspectable in a test; an SO-based event channel would move control flow into
assets where the test suite cannot see it.

---

## Project layout

```
Assets/
├─ _Project/
│  ├─ Core/                    ← asmdef Spades.Core (No Engine References)
│  │  ├─ Cards/       Suit  Rank  Card  Deck
│  │  ├─ Rules/       GameRules  LegalMoveValidator  TrickResolver
│  │  │               ScoreCalculator  HandScoreResult
│  │  ├─ State/       Seat  PlayedCard  TrickState  SeatState  TeamState
│  │  │               GameState  SeatView  ScoreSnapshot  TeamScoreLine
│  │  ├─ Flow/        GamePhase  GameLoop
│  │  ├─ Commands/    IGameCommand  Commands
│  │  ├─ Events/      IGameEvent  GameEvents  GameEventQueue
│  │  ├─ Players/     IPlayerController  HumanPlayerController  AiPlayerController
│  │  ├─ Ai/          IBiddingStrategy  HeuristicBiddingStrategy
│  │  │               ICardStrategy     HeuristicCardStrategy
│  │  │               IDrawStrategy     HeuristicDrawStrategy
│  │  └─ Util/        IRandomSource  SeededRandomSource
│  │
│  ├─ Unity/                   ← asmdef Spades.Unity → Spades.Core
│  │  ├─ Bootstrap/   GameBootstrap  LayoutSettings  GameRulesAsset
│  │  ├─ Presentation/GamePresenter  SeatNaming
│  │  ├─ Views/       CardView  HandView  TrickView  TableView  TablePosition
│  │  ├─ UI/          PanelBase  BidPanel  DrawPanel  ScoreboardView
│  │  │               HandSummaryPanel  GameOverPanel  MainMenuPanel  MessageBanner
│  │  └─ Visuals/     TweenRunner  Easing  CardArt  CardViewPool  UiFactory
│  │
│  ├─ Editor/                  ← asmdef Spades.Editor (Editor only)
│  │     SceneBuilder
│  │
│  ├─ Settings/                ← LayoutSettings + three GameRules variants
│  │
│  └─ Tests/                   ← asmdef Tests (EditMode, NUnit) → Spades.Core
│        DeckTests  TrickResolverTests  LegalMoveValidatorTests  ScoreCalculatorTests
│        BiddingTests  GameLoopTests  PresentationLoopTests  FullGameSimulationTests
│        GameHarness
│
└─ Scenes/Main.unity
```

---

## Credits

No third-party assets. Card faces, suit pips and panel shapes are generated procedurally at
runtime; text uses the LiberationSans font shipped with TextMesh Pro.
