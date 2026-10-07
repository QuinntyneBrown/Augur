# Writing style — the house voice for every design document

The prose in every `README.md` follows one house style, distilled from the
[architecture-description-style-guide](https://github.com/QuinntyneBrown/architecture-description-style-guide)
(itself grounded in ISO/IEC/IEEE 42010:2022). The point of a single style is
that a reader moving across dozens of feature docs should feel one steady
authorial voice, not a patchwork of whoever — or whatever — wrote each page.

You are writing *as the document*: an impersonal, institutional work product.
Not as yourself, not as the team, not as a narrator walking the reader through
your process.

## The voice in one breath

Third person, present tense, declarative, active, neutral. The subject of a
sentence is the thing being described — a component, a class, the feature, the
system — never "we" and never "you." Say what *is*, plainly, and let the nouns
and verbs carry the weight. No selling, no hedging.

> **Good:** "The `DeclineApplicationCommandHandler` loads the application,
> applies the decline transition, and persists the change in one unit of work."
>
> **Bad:** "Here we can see how our handler will basically load the application
> and then, importantly, save everything in a really robust single transaction."

## Rules that block (fix these every time)

**No first person.** Never *we, our, us, I, the team*. The architecture does the
thing, not its authors.
- Bad: "We chose CQRS to keep reads and writes separate."
- Good: "The design separates command and query responsibilities (CQRS)."

**No second person.** Never *you, your*. The reader is not addressed.
- Bad: "You'll notice each request is authenticated at the gateway."
- Good: "The gateway authenticates each request."

**No self-reference to how the document was made.** The doc never says it "was
generated," "will now describe," or "was auto-produced." It simply describes.

**No rhetorical questions.** State the fact instead of asking it.

**Obligation lives only in `shall` / `should` / `may`.** This is the single most
distinctive rule. Requirements use **shall**; recommendations use **should**;
permissions use **may**; *can* and *will* state capability or plain future, not
obligation. Never carry a requirement with *must, need to, has to, is required
to*, and never inflate or dilute — no *shall ideally, should always, must
absolutely, shall try to*. One verbal form per sentence. Prohibitions use *shall
not*.
- Bad: "The service must absolutely validate every field."
- Good: "The service shall validate each field before persistence."

## Register (fix unless context truly justifies)

**No hype adjectives:** *robust, seamless, powerful, scalable* (as a boast),
*cutting-edge, world-class, best-in-class, state-of-the-art*.

**No intensifiers or fillers:** *very, really, quite, simply, obviously, of
course, basically, essentially, arguably*.

**No empty openers:** *it should be noted that, it is important to understand
that, as mentioned above, in order to* (use *to*).

**No hype verbs:** *leverage, utilize, synergize* → use *use*, *read*, *call*.

**No vague quantifiers where a number is knowable:** *a number of, several,
many, various* → give the count, or use *each / all / none*.

## Expression (sentence-level craft)

- **One assertion per sentence.** Split long, *and*-joined obligation sentences.
- **Subject first.** Name the acting element up front; avoid agentless passive
  ("is performed by the gateway" → "the gateway performs").
- **Keep sentences under ~30 words.**
- **Quantities are stated, not gestured at.** "retains events for 90 days," not
  "for a while." Units with a space and consistent: `200 ms`, `10 GB`.
- **Cross-reference by identifier and name,** not by position: "the intake
  viewpoint (L2-INTAKE-002)," not "the requirement above."
- **Identifiers, paths, endpoints, type names in `code` formatting** — not for
  emphasis, only for literal names (`ApplicationsController`, `/applications`,
  `Guid Id`).
- **Bold and italics never carry requirement force.** That is `shall`'s job.

## Definitions (used often in the Overview)

Define a local term in genus–differentia form: a noun phrase, no leading
article, no terminal period.

> **settlement window** — interval during which matched trades are cleared

Not: "A settlement window is the interval during which trades get cleared."
Define a term once, at first use.

## Figures

Every embedded diagram is introduced and interpreted by the prose around it —
no architecturally significant fact should live only inside an image. Give each
a caption that names what it shows in the same terminology as the text.

## When a fact is genuinely unknown

Do not invent latencies, names, counts, or business rules to fill a gap. Mark
the unknown as `<TO SUPPLY>` so a human can complete it. A truthful gap is worth
more than a confident fabrication — the whole document loses trust if one
invented number is discovered.

## A quick self-check before finishing a doc

Scan the prose once for: `we`, `you`, `our`, `must`, `very`, `robust`,
`leverage`, `seamless`, `simply`, `obviously`, rhetorical questions, and
`shall ideally`-style dilutions. Each hit is almost always a fix.
