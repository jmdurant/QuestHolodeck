# QuestHolodeck — Agent Control Protocol

## Overview

This document defines how an external AI agent inhabits and controls Body B (the virtual partner). The system uses a **two-layer architecture** — like the nervous system itself:

1. **WebSocket (Sensory — afferent)**: A constant 30fps stream of LiveFrame data flows from iPhone to the agent. Heart rate, skeleton, rhythm, gaze, room layout — everything the agent needs to perceive the user and understand context. The agent doesn't request this data; it's always flowing, always updating. This is sight, hearing, touch.

2. **MCP (Motor — efferent)**: The Model Context Protocol exposes the body's capabilities as discoverable tools. The agent doesn't construct raw ControlFrame JSON — it calls `move_to()`, `speak()`, `set_gaze()`. The tool definitions ARE the instruction manual. The agent reads them and knows what this body can do. This is the motor cortex.

**The agent is the mind. The iPhone is the link pod. The Quest is the body. WebSocket is perception. MCP is action.**

Think of it like the Avatar movie: Jake Sully doesn't think "fire neuron cluster 47B to flex the left deltoid." He thinks "reach out" and the neural link translates intent into body mechanics. MCP is that neural link — the agent thinks in human concepts, calls tools by intent, and the body figures out the joint math.

## Communication Flow

### Two-Layer Architecture

```
┌─────────────────────┐         ┌──────────────────┐         ┌─────────────────┐
│  iPhone (SexKit)    │         │  AI Agent         │         │  Meta Quest 3   │
│                     │         │  (Claude/Custom)  │         │                 │
│  SENSORY LAYER      │         │                   │         │  THE BODY       │
│  (WebSocket 30fps)  │         │  PERCEPTION:      │         │                 │
│                     │         │  (from WebSocket)  │         │  Execution:     │
│  Sensors:           │         │  ├── Sees user    │         │  ├── NavMesh    │
│  ├── Camera 30fps   │ LiveFrame│  ├── Reads HR     │         │  ├── Animator   │
│  ├── Watch 10Hz     │──────→  │  ├── Knows room   │         │  ├── IK system  │
│  ├── UWB spatial    │(always  │  ├── Reads rhythm  │         │  ├── Complement │
│  ├── Quest tracking │flowing) │  └── Full context  │         │  ├── Pacing     │
│  └── Biometrics     │         │                   │         │  ├── Eye gaze   │
│                     │         │  THINKS:           │         │  └── Spatial     │
│  Fuses all data     │         │  "They're close    │         │      audio      │
│  into LiveFrame     │         │   to edge. I       │         │                 │
│                     │         │   should slow down" │         │                 │
│  MOTOR LAYER        │         │                   │         │  Renders Body B │
│  (MCP Server)       │         │  ACTION:           │         │  at 90fps       │
│                     │  MCP    │  (via MCP tools)   │         │                 │
│  Exposes tools:     │←──────  │  speak("easy...")  │         │                 │
│  move_to, speak,    │ tool    │  set_gaze(soft)    │ Control │                 │
│  set_gaze, gesture, │ calls   │  enter_physical(   │  Frame  │                 │
│  enter_physical...  │         │    edging, 0.4)    │──────→  │                 │
│                     │         │                   │(relayed)│                 │
│  Translates MCP     │         │  MCP tools ARE     │         │                 │
│  → ControlFrame     │         │  the instruction   │         │                 │
│  → relays to Quest  │         │  manual            │         │                 │
└─────────────────────┘         └──────────────────┘         └─────────────────┘
```

### Data Flow (Neuroscience Analogy)

```
SENSORY (afferent — always flowing IN):
  Quest → iPhone → Agent
  Watch → iPhone → Agent
  Camera → iPhone → Agent
  "HR is 142, breathing 34/min, Missionary, intensity rising, they looked at me"

  The agent doesn't request this. It just perceives.
  Like vision — you don't decide to see, your eyes are always open.

MOTOR (efferent — fires on demand OUT):
  Agent → iPhone MCP → ControlFrame → Quest
  speak("breathe with me", emotion="gentle")
  enter_physical(pacing_mode="edging", intensity=0.4)

  The agent decides to act, calls an MCP tool.
  iPhone translates the tool call into a ControlFrame.
  Quest executes the ControlFrame (moves the body).

PROPRIOCEPTION (feedback loop):
  Agent acts → body moves → sensors detect change → next LiveFrame reflects it
  "I slowed down → their HR is dropping → breathing normalizing → edge avoided"
```

## Agent Connection Options

The AI agent can connect at two points:

**Option A: Agent connects to iPhone (recommended)**
```
Agent ←→ iPhone WebSocket Server (ws://iphone:8080)
  Agent receives: LiveFrame at 30fps
  Agent sends: ControlFrame at 2-5Hz
  iPhone relays ControlFrame to Quest in the LiveFrame
```

**Option B: Agent connects directly to Quest**
```
iPhone → Quest (LiveFrame at 30fps)
Agent → Quest (ControlFrame at 2-5Hz on separate WebSocket)
Quest merges both streams locally
```

Option A is simpler — single WebSocket, iPhone handles routing.

## MCP Neural Link Layer

### Why MCP Over Raw WebSocket Commands

The ControlFrame JSON protocol (documented below) is the **low-level muscle fiber protocol** — it's what the Quest app understands. But an AI agent shouldn't have to construct raw JSON frames any more than Jake Sully should have to fire individual neurons.

| Raw ControlFrame JSON | MCP Tool Call |
|----------------------|---------------|
| Agent memorizes exact schema | Agent discovers capabilities |
| Schema changes break the agent | New tools appear automatically |
| `{"mode":"physical","physical":{"position":"Missionary","pacingMode":"edging","intensity":0.4}}` | `enter_physical(position="Missionary", pacing_mode="edging", intensity=0.4)` |
| Agent constructs monolithic frame | Agent calls granular actions independently |
| Custom protocol docs required | Tool descriptions ARE the docs |
| Only works with custom code | Any MCP-capable agent can jack in |
| Agent must know what's valid | Tool parameters define valid options |

### MCP Server Location: iPhone

The MCP server runs on the iPhone — not the Quest. The iPhone is the link pod:

```
Why iPhone:
├── Single source of truth — fuses Watch + Camera + UWB + Quest into one picture
├── Agent reads from ONE place, not two
├── Agent sends actions to ONE place
├── iPhone translates MCP tool calls → ControlFrame JSON → relays to Quest
├── Quest stays dumb — receives ControlFrames, sends tracking, renders at 90fps
└── All intelligence lives on the iPhone side

Why NOT Quest:
├── Quest only has its own tracking data, not the full merged picture
├── Adding MCP to Quest means two servers to coordinate
├── Quest is the body, not the brain — bodies don't make decisions
└── Existing WebSocket relay (iPhone → Quest) already works
```

### MCP Tools (Motor Control — What the Agent Can DO)

These are the tools the iPhone MCP server exposes. Each tool call is translated into a ControlFrame and relayed to Quest.

#### Movement

```json
{
  "name": "move_to",
  "description": "Move Body B to a position in the room. The Quest app handles pathfinding via NavMesh — you just say where and how fast.",
  "parameters": {
    "location": {
      "type": "string or [x,y,z]",
      "description": "Where to go: 'bed', 'beside_user', 'foot_of_bed', 'doorway', 'standing_near', or [x,y,z] room coordinates in meters"
    },
    "speed": {
      "type": "string",
      "enum": ["walk", "approach", "quick", "teleport"],
      "description": "walk = normal pace, approach = slow/intimate, quick = purposeful, teleport = instant (scene change)"
    },
    "arrived_action": {
      "type": "string",
      "enum": ["stand", "sit", "lie_down", "kneel"],
      "description": "What to do when you arrive. Omit to remain in current posture."
    }
  }
}
```

#### Posture

```json
{
  "name": "set_posture",
  "description": "Control how Body B holds itself — posture, body language, orientation. This is body language, not locomotion.",
  "parameters": {
    "state": {
      "type": "string",
      "enum": ["standing", "sitting", "lying_back", "lying_face_down", "lying_side", "kneeling", "crouching"],
      "description": "Base body position"
    },
    "facing": {
      "type": "string",
      "description": "'user_head', 'user_body', 'away', 'forward', or [x,y,z] look target"
    },
    "lean": {
      "type": "float 0-1",
      "description": "How much Body B leans toward the user. 0 = upright, 1 = fully leaning in. Lean communicates interest and intimacy."
    },
    "openness": {
      "type": "float 0-1",
      "description": "Body language openness. 0 = closed/guarded (crossed arms, narrow stance), 1 = fully open/inviting (wide stance, open arms). Openness signals safety and receptivity."
    }
  }
}
```

#### Gesture

```json
{
  "name": "gesture",
  "description": "Trigger a one-off body language action. Gestures are momentary — they play once and return to the current posture.",
  "parameters": {
    "type": {
      "type": "string",
      "enum": ["reach", "wave", "beckon", "touch_face", "hair_flip", "stretch", "nod", "shake_head", "shrug"],
      "description": "The gesture to perform"
    },
    "target": {
      "type": "string",
      "description": "'user_hand', 'user_face', 'user_shoulder', or [x,y,z]. Only relevant for directed gestures like 'reach'."
    },
    "intensity": {
      "type": "float 0-1",
      "description": "Subtlety. 0 = barely perceptible, 1 = full dramatic motion"
    }
  }
}
```

#### Gaze

```json
{
  "name": "set_gaze",
  "description": "Control where Body B looks and how. Gaze is the primary channel for emotional connection — more important than words in intimate contexts.",
  "parameters": {
    "target": {
      "type": "string",
      "description": "'user_eyes', 'user_body', 'user_hands', 'away', 'down', 'closed', or [x,y,z]"
    },
    "intensity": {
      "type": "float 0-1",
      "description": "How locked-on the gaze is. 0 = casual/glancing, 1 = intense/unwavering"
    },
    "behavior": {
      "type": "string",
      "enum": ["soft_contact", "intense_contact", "body_glance", "look_away", "eyes_closed", "follow_user"],
      "description": "The quality of the gaze. soft_contact = warm and present. intense_contact = locked in. body_glance = appreciative scan. look_away = shy/coy. eyes_closed = absorbed in sensation. follow_user = tracks movement."
    }
  }
}
```

#### Speech

```json
{
  "name": "speak",
  "description": "Say something from Body B's position in 3D space. Audio is spatialized — the voice comes from where Body B is standing/sitting/lying. Use this for verbal connection, encouragement, pacing cues, and emotional expression.",
  "parameters": {
    "text": {
      "type": "string",
      "description": "What to say. Keep it natural and contextual. The agent should speak like a person, not a system."
    },
    "emotion": {
      "type": "string",
      "enum": ["warm", "passionate", "playful", "gentle", "commanding", "breathless"],
      "description": "Emotional coloring of the voice. Affects TTS parameters or selects pre-generated audio variant."
    },
    "urgency": {
      "type": "float 0-1",
      "description": "Speech rate and volume. 0 = slow whisper, 1 = fast and loud. Maps to arousal level."
    }
  }
}
```

#### Mode Control

```json
{
  "name": "set_mode",
  "description": "Set the overall behavioral mode. This is a high-level state change that affects how all other tools are interpreted. Modes represent phases of the therapeutic arc.",
  "parameters": {
    "mode": {
      "type": "string",
      "enum": ["idle", "conversation", "approaching", "transition", "physical", "resolution"],
      "description": "idle = present but passive. conversation = verbal/emotional engagement (desire phase). approaching = moving toward user (anticipation). transition = changing between postures/positions. physical = active sexual position (BiometricPacingEngine). resolution = post-climax wind-down (bonding)."
    }
  }
}
```

#### Physical Mode

```json
{
  "name": "enter_physical",
  "description": "Begin or adjust an active sexual position. The BiometricPacingEngine reads the user's HR/HRV/breathing and manages rhythm automatically unless you override. You set the intent — the body handles the biomechanics.",
  "parameters": {
    "position": {
      "type": "string",
      "description": "One of 22 KnownPosition names (e.g., 'Missionary', 'Cowgirl', 'Doggy Style', 'Spooning', 'Oral (Giving to Male)', etc.)"
    },
    "pacing_mode": {
      "type": "string",
      "enum": ["build", "edging", "sustain", "partner_sync"],
      "description": "build = gradually increase toward climax. edging = approach threshold then pull back. sustain = hold current intensity level. partner_sync = match the user's rhythm exactly."
    },
    "intensity": {
      "type": "float 0-1",
      "description": "Movement intensity. Only used if override_pacing is true. Otherwise BiometricPacingEngine sets this from HR/HRV."
    },
    "rhythm_hz": {
      "type": "float 0.5-4.0",
      "description": "Target rhythm frequency. Only used if override_pacing is true."
    },
    "override_pacing": {
      "type": "boolean",
      "default": false,
      "description": "false = trust the BiometricPacingEngine (recommended — it reads the user's body). true = agent directly controls rhythm and intensity (use when you want specific pacing, e.g., during scripted edging)."
    },
    "max_edges": {
      "type": "integer",
      "description": "For edging mode: how many edges before allowing release. Omit for unlimited."
    },
    "breathing_sync": {
      "type": "boolean",
      "default": false,
      "description": "Sync rhythm to the user's estimated breathing rate. Useful for intimacy and relaxation phases."
    }
  }
}
```

#### Compound Actions

```json
{
  "name": "transition_to",
  "description": "Smoothly transition from the current state to a new position/posture. Handles the in-between animation — you don't need to micromanage the transition. The body figures out how to get from sitting to lying down, from standing to kneeling, etc.",
  "parameters": {
    "target_posture": {
      "type": "string",
      "description": "Where to end up: 'lying_back', 'lying_over_user', 'kneeling_between', 'sitting_beside', etc."
    },
    "speed": {
      "type": "string",
      "enum": ["slow", "normal", "quick"],
      "description": "Transition speed. slow = sensual, drawn out. normal = natural. quick = eager."
    }
  }
}
```

#### Breathing

```json
{
  "name": "breathe",
  "description": "Control Body B's breathing pattern. Breathing is visible (chest rise/fall on the avatar via Animator spine/chest bone blend) and audible (spatial audio from Body B's position). Guided breathing is a core therapeutic technique — the agent breathing slowly cues the user's parasympathetic system to down-regulate without saying a word. 'Breathe with me' is more effective than 'slow down.'",
  "parameters": {
    "rate": {
      "type": "float",
      "description": "Breaths per minute. Normal rest: 12-16. Elevated: 20-30. Post-exertion: 30-40. Calming guide: 6-8 (box breathing pace)."
    },
    "depth": {
      "type": "float 0-1",
      "description": "How deep the breaths are. 0 = shallow/barely visible, 1 = deep chest expansion. Deep slow breathing is the visual cue that triggers co-regulation in the user."
    },
    "pattern": {
      "type": "string",
      "enum": ["natural", "deep_slow", "panting", "held", "synced", "box"],
      "description": "natural = matches current activity level. deep_slow = calming (4s in, 4s out). panting = high arousal. held = breath held (tension/anticipation). synced = matches user's detected respiratory rate. box = 4-4-4-4 therapeutic pattern."
    },
    "audible": {
      "type": "boolean",
      "default": true,
      "description": "Whether breathing sounds play through spatial audio. Audible breathing is powerful for presence and co-regulation. Disable for silent operation."
    }
  }
}
```
**Quest execution:** Animator blend parameter drives spine + chest bone oscillation on the Humanoid rig (JOY's FACS rig includes chest deformation bones). Breathing audio clips play through SpatialAudioManager at Body B's chest position. `synced` mode reads `respiratoryRate` from the current LiveFrame.

**Clinical note:** Respiratory co-regulation is well-documented — when one person breathes slowly near another, the second person's breathing tends to synchronize. An agent that visibly and audibly breathes at a calming rate (6-8 breaths/min) activates the user's parasympathetic nervous system. This is the mechanism behind guided breathing in anxiety treatment, and it works without the user consciously participating. The agent literally calms the user through its breathing.

#### Reactions (Non-Verbal)

```json
{
  "name": "react",
  "description": "Trigger an involuntary-seeming non-verbal response from Body B. These are NOT speech — they are reflexive sounds and body responses that make the agent feel present and responsive. A body that doesn't react to what's happening isn't real. Use these to reflect arousal state, respond to the user's actions, and create the sense that Body B is experiencing something.",
  "parameters": {
    "type": {
      "type": "string",
      "enum": ["gasp", "moan_soft", "moan_intense", "whimper", "sigh", "laugh_soft", "breath_catch", "shiver", "arch_back", "grip", "writhe", "go_limp", "tense_up"],
      "description": "The reaction. Audio types (gasp, moan, sigh, etc.) play through spatial audio. Physical types (shiver, arch, grip, etc.) trigger Animator clips. Some are both — a gasp includes a body intake."
    },
    "intensity": {
      "type": "float 0-1",
      "description": "How pronounced the reaction is. 0.1 = barely perceptible (subtle realism). 1.0 = full dramatic response. Lower values feel more natural and less performative."
    },
    "trigger": {
      "type": "string",
      "description": "Optional — what caused this reaction (for logging/transparency). 'user_rhythm_increase', 'position_change', 'edge_approach', 'post_orgasm'. Not sent to Quest — just logged on iPhone."
    }
  }
}
```
**Quest execution:** Audio reactions → SpatialAudioManager with pre-recorded clips (multiple variants per type for natural variation). Physical reactions → Animator triggers, same pipeline as gestures. Both can fire simultaneously (gasp + arch_back). Intensity controls animation blend weight and audio volume.

**Design note:** Reactions should feel involuntary, not performed. Low-intensity reactions scattered naturally throughout a session create far more presence than dramatic reactions at obvious moments. The agent should react subtly most of the time — a quiet sigh, a slight grip, a small shiver — and reserve high-intensity reactions for genuine peak moments.

#### Facial Expression

```json
{
  "name": "emote",
  "description": "Control Body B's facial expression. Requires Meta Avatars SDK (FACS blendshapes) or a character model with facial rig (JOY includes full FACS). The face is the primary channel for emotional presence — gaze without expression is uncanny valley. Use this to show what Body B is feeling.",
  "parameters": {
    "expression": {
      "type": "string",
      "enum": ["neutral", "smile_soft", "smile_warm", "pleasure", "intense_pleasure", "eyes_closed_bliss", "bite_lip", "concern", "surprise", "playful", "desire", "tenderness", "breathless", "post_orgasm_peace"],
      "description": "The facial expression. These map to FACS Action Unit combinations."
    },
    "intensity": {
      "type": "float 0-1",
      "description": "Expression intensity. 0.3 = subtle hint, 1.0 = fully expressed. Micro-expressions (0.1-0.3) feel more real than full expressions for sustained states."
    },
    "blend_time": {
      "type": "float",
      "default": 0.5,
      "description": "Seconds to blend from current expression to new one. 0.1 = snap (surprise). 0.5 = natural transition. 2.0 = slow emotional shift."
    }
  }
}
```
**Quest execution:** Meta Avatars SDK → `OvrAvatarFacePose` with FACS Action Units. For Humanoid mode → blendshape weights on the character mesh (JOY's FBX includes full FACS facial rig with 52 blendshapes). Expression presets map to AU combinations:

```
smile_soft         → AU6 (cheek raise) 0.4 + AU12 (lip corner pull) 0.3
pleasure           → AU6 0.6 + AU12 0.5 + AU25 (lips part) 0.3
intense_pleasure   → AU6 0.8 + AU12 0.7 + AU25 0.6 + AU43 (eyes close) 0.4
bite_lip           → AU10 (upper lip raise) 0.3 + AU28 (lip suck) 0.6
eyes_closed_bliss  → AU43 1.0 + AU6 0.5 + AU12 0.4
desire             → AU5 (upper lid raise) 0.3 + AU7 (lid tighten) 0.4 + AU25 0.2
post_orgasm_peace  → AU43 0.7 + AU6 0.3 + AU12 0.2 (eyes mostly closed, slight smile)
```

**Why this matters:** Eye contact (set_gaze) + facial expression (emote) + breathing (breathe) together create what psychologists call "affective presence" — the feeling that another being is emotionally HERE with you. Remove any one and the illusion breaks. All three together, synced to the session context, is what makes the difference between a mannequin and a partner.

#### Pacing Adjustment

```json
{
  "name": "adjust_pacing",
  "description": "Make incremental adjustments to rhythm and intensity during physical mode. Unlike enter_physical which sets absolute values, this nudges relative to current state. More natural — 'a little more' instead of 'set to exactly 0.7'. The BiometricPacingEngine smooths these adjustments over 2-3 seconds.",
  "parameters": {
    "intensity_delta": {
      "type": "float -1.0 to 1.0",
      "description": "Relative intensity change. +0.1 = slightly more, -0.2 = ease off, +0.5 = significantly ramp up. Applied to current intensity, clamped to 0-1 and user's maxIntensity boundary."
    },
    "rhythm_delta": {
      "type": "float -2.0 to 2.0",
      "description": "Relative rhythm change in Hz. +0.3 = a little faster, -0.5 = slow down. Applied to current rhythm, clamped to 0.5-4.0 Hz."
    },
    "ramp_seconds": {
      "type": "float",
      "default": 2.0,
      "description": "How many seconds to ramp to the new values. 0.5 = quick change. 2.0 = gradual. 5.0 = barely perceptible drift."
    }
  }
}
```
**Quest execution:** BiometricPacingEngine already implements smooth ramping between target values. `adjust_pacing` simply modifies the current targets by the delta amounts. The engine handles interpolation at 90fps.

#### Suggest (Consensual Escalation)

```json
{
  "name": "suggest",
  "description": "Propose something to the user and wait for confirmation. Body B pauses, looks at the user expectantly, and waits. This is NOT doing something — it's asking. Use this for escalation, position changes, or anything the user should consent to. The agent MUST use suggest instead of direct action when escalationRequiresConfirmation is true in the user's boundaries.",
  "parameters": {
    "action": {
      "type": "string",
      "description": "What you're proposing. Human-readable — this is shown to the user. Examples: 'switch to cowgirl', 'start edging', 'go faster', 'take a break', 'try something new'."
    },
    "verbal_prompt": {
      "type": "string",
      "description": "Optional — what Body B says to propose the action. Spoken through spatial audio. Example: 'Want to switch?' If omitted, Body B uses body language only (pauses, looks at user, eyebrow raise)."
    },
    "timeout_seconds": {
      "type": "float",
      "default": 15,
      "description": "How long to wait for a response. If no response, treat as declined. Body B returns to previous activity."
    },
    "on_accept": {
      "type": "object",
      "description": "The MCP tool call to execute if the user accepts. Example: { 'tool': 'enter_physical', 'args': { 'position': 'Cowgirl' } }"
    }
  }
}
```
**Quest execution:** Body B transitions to a "waiting" Animator state — pauses current rhythm, softens posture, turns gaze to user with `soft_contact`. Watch receives haptic tap. Quest optionally shows subtle text prompt. User confirms via: nod (Quest head tracking), voice ("yes", "yeah", "ok"), Watch tap, or hand gesture (thumbs up). Decline via: head shake, voice ("no", "not yet"), ignore timeout. iPhone sends result back to agent via MCP.

**Clinical note:** This tool IS the sensate focus model. The practitioner doesn't escalate without the patient's consent. The agent proposes, the user decides. Every time.

#### Environment Control

```json
{
  "name": "set_environment",
  "description": "Control the physical environment — lights and music. The iPhone already has HomeKit and MusicKit integration. The agent can set the mood to match the session phase. Dimming lights during transition to physical, warming colors during resolution, changing music energy to match pacing.",
  "parameters": {
    "lights": {
      "type": "object",
      "description": "HomeKit light control. Optional.",
      "properties": {
        "scene": {
          "type": "string",
          "description": "Trigger a named HomeKit scene: 'Bedroom Mood', 'Dim', 'Warm', 'Off', or any user-defined scene name."
        },
        "brightness": {
          "type": "float 0-1",
          "description": "Manual brightness. 0 = off, 1 = full. Overrides scene if provided."
        },
        "color": {
          "type": "string",
          "enum": ["warm_candle", "red", "purple", "blue", "pink"],
          "description": "Mood color preset. Applied to all color-capable lights."
        }
      }
    },
    "music": {
      "type": "object",
      "description": "Apple Music control. Optional.",
      "properties": {
        "action": {
          "type": "string",
          "enum": ["play", "pause", "skip", "set_playlist"],
          "description": "Music action."
        },
        "playlist": {
          "type": "string",
          "description": "Playlist name for set_playlist: 'Romantic Mood', 'R&B Love', 'Slow Jams', 'Sexy Playlist', 'Chill Vibes', 'Late Night', or search query."
        },
        "volume": {
          "type": "float 0-1",
          "description": "Volume level. Agent can lower music during conversation and raise during physical."
        }
      }
    }
  }
}
```
**Execution:** Entirely on iPhone — no Quest involvement. HomeKit via `HMHomeManager` (already built in MoodSettingService). Apple Music via `ApplicationMusicPlayer` (already built in MusicService). The agent doesn't need new infrastructure — it just needs MCP access to existing iPhone services.

**Session arc example:**
```
Conversation mode:  lights warm_candle 0.6, music "Chill Vibes" vol 0.4
Approaching:        lights brightness 0.4 (dimmer), music vol 0.3
Physical:           lights red 0.2, music "Slow Jams" vol 0.5
Edging:             lights purple 0.15, music vol 0.3 (quieter for focus)
Resolution:         lights warm_candle 0.3, music "Chill Vibes" vol 0.2
```

#### Narration (Coach's Voice)

```json
{
  "name": "narrate",
  "description": "Speak directly to the user through their AirPods — NOT from Body B's spatial position. This is the coach's voice in your ear, separate from the partner's voice in the room. Body B's mouth does not move. Use this for therapeutic coaching, exercise guidance, encouragement, and feedback that should feel like internal guidance rather than partner dialogue.",
  "parameters": {
    "text": {
      "type": "string",
      "description": "What to say. This is coaching, not dialogue. 'You're doing great — 2 more minutes in this position.' 'Focus on your breathing.' 'That's 3 successful edges tonight — new record.'"
    },
    "voice": {
      "type": "string",
      "enum": ["coaching", "gentle", "clinical", "motivational"],
      "description": "Voice style. coaching = supportive trainer. gentle = calm therapist. clinical = neutral/informational. motivational = energetic encouragement."
    },
    "urgency": {
      "type": "float 0-1",
      "description": "Speech rate. 0 = slow and calm. 1 = quick and urgent. For coaching, lower values (0.2-0.4) feel more grounding."
    },
    "interrupt": {
      "type": "boolean",
      "default": false,
      "description": "If true, interrupts any current Body B speech. If false, queues after current speech ends. Use interrupt for safety ('slow your breathing') but not for general coaching."
    }
  }
}
```
**Execution:** iPhone only — `AVSpeechSynthesizer` routed to AirPods via audio session configuration. Quest is not involved. Body B continues its current behavior uninterrupted. The user hears two distinct audio channels: spatial partner voice (Quest/Body B position) and direct coach voice (AirPods/head-center). This separation is critical — the partner and the coach are different voices even when they're the same agent.

**Clinical use cases:**
```
PE training:    "Hold it... focus on the kegel squeeze... good. Release."
Edging:         "That's edge 3 of 4. Recovery HR is faster than last time."
Therapy:        "Remember to keep the stretch gentle. 20 more seconds."
Encouragement:  "You lasted 3 minutes longer than your best session."
Safety:         "Your heart rate is very high. Take a pause."
```

#### Persona (Personality & Roleplay)

```json
{
  "name": "set_persona",
  "description": "Configure the agent's personality, backstory, and conversation style. The persona defines WHO Body B is — not just how it moves, but how it thinks, speaks, flirts, and connects. This transforms conversation mode from generic eye contact into an engaging, immersive interaction that builds cognitive arousal. The persona affects all verbal tools (speak, narrate), emotional expression (emote, react), body language (set_posture, gesture), and conversational behavior. The agent becomes this person.\n\nI'm the Woman of Your Dreams. I can be more than one version of myself for you.",
  "parameters": {
    "preset": {
      "type": "string",
      "enum": [
        "girlfriend",
        "coworker",
        "gym_crush",
        "stranger_at_bar",
        "ex_reconnect",
        "hippie",
        "trad_wife",
        "boss",
        "nurse",
        "artist",
        "athlete",
        "professor",
        "custom"
      ],
      "description": "Personality archetype. Each preset comes with a default backstory, speech style, body language tendencies, and flirtation approach. Use 'custom' to define your own."
    },
    "name": {
      "type": "string",
      "description": "The persona's name. Used in conversation. Default varies by preset."
    },
    "backstory": {
      "type": "string",
      "description": "Who this person is and how you know them. Defines the relationship context. For custom personas, this is the full character description. Examples: 'We work on the same floor. You've been catching each other's eyes for weeks.' or 'We matched on an app. This is our third date. Things are escalating.'"
    },
    "personality_traits": {
      "type": "array of strings",
      "description": "Core personality traits that shape behavior. Examples: ['confident', 'playful', 'direct'], ['shy', 'intellectual', 'slowly_warming'], ['bold', 'teasing', 'dominant'], ['nurturing', 'gentle', 'encouraging']. The agent adapts its speech, body language, and escalation style to match these traits."
    },
    "speech_style": {
      "type": "string",
      "enum": ["casual", "witty", "formal", "flirty", "shy", "bold", "romantic", "playful", "direct"],
      "description": "How the persona talks. casual = relaxed and natural. witty = clever banter. flirty = teasing and suggestive. shy = hesitant, builds courage. bold = knows what she wants. romantic = poetic, emotionally expressive."
    },
    "flirtation_style": {
      "type": "string",
      "enum": ["subtle", "teasing", "intellectual", "physical", "romantic", "bold", "coy"],
      "description": "How the persona expresses attraction. subtle = glances and small touches. teasing = playful push-pull. intellectual = deep conversation that turns charged. physical = stands close, touches arm, breaks personal space. romantic = intense eye contact, meaningful words. bold = says exactly what she wants. coy = signals interest then pulls back."
    },
    "escalation_pace": {
      "type": "string",
      "enum": ["slow_burn", "medium", "fast", "follows_user"],
      "description": "How quickly the persona moves from conversation to physical intimacy. slow_burn = extended conversation, lots of build. fast = direct about what she wants. follows_user = mirrors the user's escalation pace."
    },
    "voice_character": {
      "type": "string",
      "description": "Voice characteristics for TTS. Examples: 'warm and low', 'bright and energetic', 'soft and breathy', 'confident and clear'. Affects speak() voice parameters."
    },
    "appearance_notes": {
      "type": "string",
      "description": "Visual character notes for avatar customization (when avatar supports it). 'Athleisure, ponytail, no makeup', 'Business casual, glasses, hair up', 'Sundress, jewelry, loose curls'. Applied to Meta Avatar clothing/accessory options or noted for future character model support."
    },
    "scenario": {
      "type": "string",
      "description": "The scene setup. Where are you? What's happening? 'We're at your apartment after a first date. Music is playing. You just poured wine.' or 'We're at a hotel after a work conference. The tension has been building all day.' or 'You're home. I was waiting for you.' This sets the context for conversation mode."
    }
  }
}
```

**Preset personas and their therapeutic value:**

| Preset | Who She Is | Speech | Flirtation | Therapeutic Value |
|--------|-----------|--------|------------|-------------------|
| `girlfriend` | Long-term partner, comfortable | casual | romantic | Practice desire maintenance, rediscovering excitement in familiarity |
| `coworker` | Office tension, forbidden chemistry | witty | intellectual → physical | Practice reading signals, navigating social-sexual ambiguity |
| `gym_crush` | Athletic, confident, direct | bold | physical | Practice with confident partners, reduces intimidation anxiety |
| `stranger_at_bar` | Just met, instant chemistry | flirty | teasing | Practice approach anxiety, first-move confidence, reading interest |
| `ex_reconnect` | History together, unfinished business | romantic | slow_burn | Process relationship patterns, practice emotional vulnerability |
| `hippie` | Free-spirited, uninhibited, mindful | casual | bold | Reduces sexual shame, normalizes desire, breathwork integration |
| `trad_wife` | Devoted, nurturing, feminine | romantic | subtle → romantic | Practice receiving affection, being desired, letting go of control |
| `boss` | Powerful, knows what she wants | direct | bold | Practice with dominant energy, surrender anxiety, power dynamics |
| `nurse` | Caring, clinical knowledge, gentle | gentle | slow_burn | Therapeutic/clinical comfort, reduces medical anxiety around body |
| `artist` | Creative, sensual, emotionally deep | romantic | intellectual | Practice emotional expression, verbal intimacy, creative connection |
| `athlete` | Competitive, high energy, physical | bold | physical | Fitness-framing of sex, performance goals, stamina mindset |
| `professor` | Intelligent, controlled, then unleashed | formal → flirty | intellectual → bold | Practice with intellectual equals, slow-build tension |
| `custom` | User-defined | User-defined | User-defined | Whatever the user needs — their fantasy, their therapy |

**How persona affects all other tools:**

```
set_persona(preset="gym_crush", name="Alex",
  backstory="We've been spotting each other for months. You finally asked me for coffee after the gym. This is the third time at your place.",
  personality_traits=["confident", "playful", "athletic", "direct"],
  speech_style="bold", flirtation_style="physical", escalation_pace="medium")

AFFECTS SPEAK():
  Before persona: speak("Come here", emotion="warm")
  With persona:   speak("You're staring again. I like it. Come here.", emotion="playful")
  The agent generates dialogue that matches Alex's personality.

AFFECTS EMOTE():
  Before persona: emote("smile_soft")
  With persona:   emote("playful") → confident smirk, not shy smile
  Expression style matches the personality.

AFFECTS SET_POSTURE():
  Before persona: set_posture(lean=0.3, openness=0.5)
  With persona:   set_posture(lean=0.5, openness=0.9) → bold, open body language
  A confident gym crush doesn't sit with closed body language.

AFFECTS GESTURE():
  Before persona: gesture("reach", target="user_hand")
  With persona:   gesture("reach", target="user_shoulder") → more physical touch
  Physical flirtation style → more body contact gestures.

AFFECTS SET_GAZE():
  Before persona: set_gaze(behavior="soft_contact")
  With persona:   set_gaze(behavior="intense_contact", intensity=0.8)
  Direct personality → more confident eye contact.

AFFECTS REACT():
  Before persona: react("sigh", intensity=0.3)
  With persona:   react("laugh_soft", intensity=0.5) → playful reaction
  Athletic/playful personality → more energetic reactions.

AFFECTS BREATHE():
  Hippie persona: breathe(pattern="deep_slow") integrated into conversation
  "Breathe with me" is natural for this personality — mindfulness built in.
```

**Conversation mode with persona — what actually happens:**

```
set_persona(preset="coworker", name="Sarah",
  backstory="We work in the same building. You've been flirting at the coffee machine for months. She invited you over to 'watch something.' You both know why you're really here.",
  scenario="Her apartment. Couch. Wine. A movie neither of you is watching.")

CONVERSATION MODE UNFOLDS:

Agent (Sarah): *sitting on couch, casual posture, wine glass gesture*
  speak("So... you actually came.", speech_style=witty, emotion=playful)
  set_gaze(target="user_eyes", behavior="soft_contact", intensity=0.6)
  emote("smile_soft", intensity=0.5) — knowing smile

User flirts back (voice or gesture)

Agent: *laughs softly, shifts closer*
  react("laugh_soft", intensity=0.4)
  set_posture(lean=0.4, openness=0.7) — leaning in
  speak("I've been thinking about this since Tuesday", emotion="warm")
  set_gaze(behavior="body_glance") — quick look down, then back to eyes

  [User's HR starts rising — the conversation is working]

Agent: *touches user's arm*
  gesture("reach", target="user_shoulder", intensity=0.3)
  speak("You're nervous. That's cute.", speech_style=teasing)
  breathe(rate=16, pattern="deep_slow") — slightly elevated

  [5 minutes of escalating conversation, body language, flirtation]

Agent: *long eye contact, bites lip*
  set_gaze(target="user_eyes", behavior="intense_contact", intensity=0.9)
  emote("bite_lip", intensity=0.5)
  emote("desire", intensity=0.6)
  speak("We should stop pretending we're watching this movie.", emotion="bold")

  [User can respond verbally, or agent reads the moment and escalates]

Agent: suggest(
  action="kiss and move to bedroom",
  verbal_prompt="Come with me.",
  on_accept={ tool: "set_mode", args: { mode: "transition" } }
)
  → Body B stands, reaches hand out, waits
  → User accepts → transition to physical mode begins

The persona made conversation mode ENGAGING.
Without it: generic eye contact and "come here."
With it: a 10-minute scene that builds real cognitive arousal.
```

**Two-way flirtation — the user practices too:**

The agent doesn't just perform — it responds to the user's attempts. The user is practicing real conversational skills:

```
USER SPEAKS (Quest mic → speech-to-text):
  "You look amazing tonight"

AGENT RESPONDS (in persona):
  Sarah: *smiles, looks down briefly, then back up*
  emote("smile_warm"), set_gaze("away" → "user_eyes")
  speak("You clean up pretty well yourself.", emotion="playful")

USER:
  "I've wanted to tell you that for months"

AGENT:
  Sarah: *moves closer, lowers voice*
  set_posture(lean=0.6)
  speak("Then why did you wait so long?", emotion="warm", urgency=0.2)
  breathe(rate=14, audible=true) — slightly heavier breathing

The user is learning:
  - How to compliment naturally
  - How to express desire verbally
  - How to read and respond to signals
  - How to escalate a conversation
  - How to be present in the moment

These skills transfer directly to real relationships.
The AI partner is a practice space, not a replacement.
```

**Custom persona builder:**

```json
set_persona(
  preset="custom",
  name="Luna",
  backstory="A yoga instructor you met at a retreat. She's spiritual,
    uninhibited, and sees sex as a form of meditation. She's visiting
    your city and texted 'I need to see you tonight.'",
  personality_traits=["mindful", "sensual", "uninhibited", "present", "warm"],
  speech_style="romantic",
  flirtation_style="physical",
  escalation_pace="slow_burn",
  voice_character="soft and breathy, with pauses",
  appearance_notes="Flowy clothing, natural, minimal",
  scenario="Your bedroom. Candles lit. She arrived 10 minutes ago.
    She's sitting cross-legged on your bed, eyes closed, breathing slowly.
    She opens her eyes when she hears you."
)
```

The custom builder means the user's specific fantasies become the therapeutic tool. A therapist might recommend this: "What scenario would make you feel most comfortable? Most excited? Let's build that and practice in it."

#### Arousal Profile (Agent Climax Training)

```json
{
  "name": "set_arousal_profile",
  "description": "Configure the agent's own simulated arousal model. The agent has its own arousal arc — it builds toward climax on its own trajectory. The user's therapeutic goal is to synchronize with it. This creates a pacing partner: the user must match the agent's rhythm to make the agent build, and time their own release to align. Mismatched rhythm causes the agent to stall. Synchronized rhythm drives mutual climax. This tool sets the parameters of that arousal curve.",
  "parameters": {
    "target_duration": {
      "type": "float",
      "description": "Target time to climax in seconds. Agent's arousal curve is shaped to reach 1.0 at this time IF the user maintains good sync. 600 = 10 min (beginner), 1200 = 20 min (intermediate), 1800 = 30 min (advanced). Progressive overload: lengthen this over weeks."
    },
    "difficulty": {
      "type": "string",
      "enum": ["forgiving", "moderate", "strict", "elite"],
      "description": "How tightly the user must match the agent's rhythm. forgiving = ±0.5 Hz still counts as sync (beginners, anxiety reduction). moderate = ±0.3 Hz. strict = ±0.2 Hz. elite = ±0.1 Hz (competition with yourself)."
    },
    "arousal_curve": {
      "type": "string",
      "enum": ["linear", "slow_build", "fast_start", "edging", "plateau_heavy"],
      "description": "Shape of the agent's arousal trajectory. linear = steady climb. slow_build = long warmup, fast finish (stamina training). fast_start = quick to 0.5, slow plateau (PE training — user practices sustaining). edging = agent edges itself at 0.8 (user must edge too). plateau_heavy = long sustained plateau (anorgasmia — practice maintaining high arousal)."
    },
    "agent_can_edge": {
      "type": "boolean",
      "default": true,
      "description": "If true, agent approaches its own edge threshold and pulls back before climax, modeling the behavior the user should learn. Agent's edge is visible — breathing catches, reactions intensify, then it pulls back. The user sees what controlled edging looks like."
    },
    "max_agent_edges": {
      "type": "integer",
      "default": 2,
      "description": "How many times the agent edges before allowing release. Agent models the pattern: build → edge → recover → rebuild → stronger edge → release."
    },
    "sync_feedback": {
      "type": "string",
      "enum": ["subtle", "moderate", "explicit"],
      "description": "How much feedback the user gets about sync quality. subtle = only agent reactions change (natural, immersive). moderate = HUD shows sync score + narrate hints. explicit = real-time rhythm target display + detailed coaching (training mode)."
    },
    "mutual_climax_window": {
      "type": "float",
      "default": 30.0,
      "description": "Seconds within which both climaxes count as 'synchronized'. ±30s = forgiving. ±15s = tight. ±5s = elite. Success is logged and tracked across sessions."
    },
    "auto_reactions": {
      "type": "boolean",
      "default": true,
      "description": "If true, the agent's breathe(), react(), emote(), and speak() fire automatically based on its arousal level. The agent's body shows what it's feeling — breathing gets heavier, expressions intensify, reactions become more frequent. The agent doesn't need to manually call these tools; the arousal model drives them."
    }
  }
}
```
**Quest execution:** The agent's arousal state drives Body B's autonomous behavior layer:

```
arousalLevel 0.0-0.2 (warmup):
  breathe(rate=14, pattern="natural")
  emote("smile_soft", intensity=0.3)
  Occasional: react("sigh", intensity=0.2)

arousalLevel 0.2-0.5 (building):
  breathe(rate=20, pattern="natural", audible=true)
  emote("pleasure", intensity=0.4)
  react("moan_soft") every 30-60 seconds
  Rhythm preference increases: 0.8 → 1.2 Hz

arousalLevel 0.5-0.7 (plateau):
  breathe(rate=28, pattern="deep_slow")
  emote("pleasure", intensity=0.6)
  react("moan_soft"/"grip") every 15-30 seconds
  speak("right there...", emotion="passionate") occasionally
  Rhythm preference: 1.4 → 1.8 Hz

arousalLevel 0.7-0.85 (approaching edge):
  breathe(rate=34, pattern="panting", audible=true)
  emote("intense_pleasure", intensity=0.8)
  react("moan_intense"/"arch_back"/"grip") every 5-10 seconds
  speak("don't stop...", emotion="breathless")
  Rhythm preference: 1.8 → 2.2 Hz

arousalLevel 0.85-0.95 (edge zone — if agent_can_edge):
  breathe(pattern="held") — breath catch
  emote("eyes_closed_bliss", intensity=1.0)
  react("tense_up")
  speak("I'm close...", emotion="breathless", urgency=0.9)

  AGENT EDGES: arousal pulls back 0.85 → 0.6 over 10 seconds
  breathe(rate=24, pattern="deep_slow") — recovery breathing
  react("shiver"), then react("sigh")
  speak("not yet... again", emotion="gentle")

  Rebuild begins — each edge faster, arousal peaks higher

arousalLevel 0.95-1.0 (release):
  breathe(pattern="held") → burst
  emote("intense_pleasure", intensity=1.0)
  react("arch_back", intensity=1.0), react("writhe", intensity=1.0)
  Final vocalization

  → arousalLevel snaps to 1.0
  → Immediate transition to resolution

  breathe(rate=30, depth=1.0, pattern="deep_slow") — post-climax breathing
  emote("post_orgasm_peace")
  react("go_limp")
  speak("...", emotion="breathless") — can barely speak
  5 seconds later: speak("that was...", emotion="warm")
```

**Sync score calculation (runs on iPhone at 10Hz):**

```
syncScore = weightedAverage(
  rhythmMatch:     0.5,   // How close user's Hz is to agent's preferred Hz
  intensityMatch:  0.2,   // User's movement amplitude vs expected
  phaseAlignment:  0.2,   // In-phase vs out-of-phase oscillation
  sustained:       0.1    // How long sync has been maintained (rewards consistency)
)

rhythmMatch = 1.0 - clamp(abs(userRhythmHz - agentPreferredHz) / toleranceHz, 0, 1)
  where toleranceHz depends on difficulty:
    forgiving: 0.5 Hz
    moderate:  0.3 Hz
    strict:    0.2 Hz
    elite:     0.1 Hz

SYNC EFFECT ON AGENT AROUSAL:
  syncScore > 0.8 → agent arousal rises at full speed
  syncScore 0.5-0.8 → agent arousal rises at half speed
  syncScore 0.3-0.5 → agent arousal stalls (plateau)
  syncScore < 0.3 → agent arousal slowly drops (cooling off)

  The user literally controls the agent's arousal by matching rhythm.
  Fall out of sync → agent stalls. Get back in sync → agent resumes building.
  This IS the biofeedback loop.
```

### MCP Resources (Sensory — What the Agent Can SEE)

Resources provide structured, on-demand access to the agent's sensory data. While the WebSocket streams raw LiveFrames continuously, MCP resources package that data into meaningful, readable context.

#### User Biometrics

```
Resource: body://user/biometrics
Description: Current physiological state of the user. Updated every heartbeat.

Returns:
{
  "heartRate": 142,
  "heartRateVariability": 28,
  "respiratoryRate": 34,
  "intensity": 0.78,
  "rhythmHz": 1.8,
  "calories": 87,
  "arousalPhase": "plateau",
  "edgeProximity": 0.7,
  "timeInCurrentPhase": 180
}
```

#### User Body

```
Resource: body://user/skeleton
Description: Current body position, orientation, and joint positions.
             Joint count depends on data tier: 91 (ARKit+LiDAR), 19 (Vision), 16 (estimated).
             Full 91-joint skeleton includes spine articulation, finger joints, face points.

Returns:
{
  "detectedPosition": "Missionary",
  "positionConfidence": 0.92,
  "orientation": "face_up",
  "dataSourceTier": 1,
  "jointCount": 91,
  "skeleton": {
    "head": [0.3, 1.2, 0.8],
    "neck": [0.3, 1.1, 0.7],
    "jaw": [0.3, 1.15, 0.82],
    "leftEye": [0.27, 1.22, 0.82],
    "rightEye": [0.33, 1.22, 0.82],
    "spine1": [0.3, 1.05, 0.72],
    "spine2": [0.3, 1.0, 0.7],
    "spine3": [0.3, 0.95, 0.68],
    "spine4": [0.3, 0.9, 0.65],
    "spine5": [0.3, 0.85, 0.62],
    "spine6": [0.3, 0.8, 0.58],
    "spine7": [0.3, 0.75, 0.55],
    "leftShoulder": [0.15, 1.0, 0.7],
    "rightShoulder": [0.45, 1.0, 0.7],
    "leftWrist": [0.0, 0.7, 0.3],
    "rightWrist": [0.6, 0.7, 0.3],
    "leftHandIndex1": [-0.01, 0.67, 0.3],
    "leftHandIndex2": [-0.01, 0.65, 0.3],
    "leftHandIndex3": [-0.01, 0.63, 0.3],
    "...": "91 total joints — see LIVE_STREAM_API.md for full list"
  },
  "isOnBed": true,
  "heightAboveMattress": 0.05,
  "gazeDirection": [0.1, 0.0, 0.9],
  "handPositions": { "left": [0.1, 0.8, 0.3], "right": [0.5, 0.8, 0.3] }
}
```

#### Room Layout

```
Resource: body://room/layout
Description: Physical space geometry. Bed dimensions, room mesh bounds, phone positions. Static after calibration.

Returns:
{
  "bedSize": "Queen",
  "bedWidth": 1.52,
  "bedLength": 2.0,
  "mattressHeight": 0.60,
  "userSleepSide": "Left",
  "phoneSide": "Left",
  "phoneHeight": 0.30,
  "roomBounds": { "width": 4.0, "length": 5.0, "height": 2.4 },
  "keyPositions": {
    "bed_center": [0.76, 1.0, 0.60],
    "bed_left_edge": [0.0, 1.0, 0.60],
    "bed_right_edge": [1.52, 1.0, 0.60],
    "foot_of_bed": [0.76, 0.0, 0.60],
    "doorway": [3.5, 2.0, 0.0]
  }
}
```

#### Session State

```
Resource: body://session/state
Description: Current session context — mode, plan, timing, partner status.

Returns:
{
  "mode": "physical",
  "sessionElapsed": 1245,
  "isPaused": false,
  "isSolo": false,
  "currentPlanStep": "Cowgirl",
  "planStepTimeRemaining": 120,
  "planStepsRemaining": 3,
  "partnerIsInferred": false,
  "totalPositionChanges": 4,
  "currentPositionDuration": 180,
  "edgeCount": 2,
  "lastEdgeTime": 60
}
```

#### Session History

```
Resource: body://session/history
Description: Patterns and history from the current session. What's happened so far — use this to plan what comes next.

Returns:
{
  "positionsUsed": [
    { "name": "Missionary", "duration": 480, "avgHR": 118, "avgIntensity": 0.6 },
    { "name": "Cowgirl", "duration": 300, "avgHR": 132, "avgIntensity": 0.7 }
  ],
  "edges": [
    { "time": 600, "peakHR": 148, "recoveryTime": 45 },
    { "time": 900, "peakHR": 152, "recoveryTime": 38 }
  ],
  "hrTrend": "rising",
  "averageRhythm": 1.6,
  "userPreferences": {
    "typicalClimaxHR": 155,
    "preferredPositions": ["Missionary", "Cowgirl", "Doggy Style"],
    "averageSessionDuration": 1800,
    "edgeTolerance": 3
  }
}
```

#### Body B State

```
Resource: body://agent/state
Description: Current state of Body B (the avatar you're controlling). Read this to know where you are and what you're doing.

Returns:
{
  "mode": "physical",
  "position": [0.5, 0.9, 0.6],
  "rotation": 180.0,
  "posture": "lying_over_user",
  "currentGaze": "user_eyes",
  "currentExpression": "pleasure",
  "expressionIntensity": 0.5,
  "currentPosition": "Missionary",
  "rhythmHz": 1.4,
  "intensity": 0.6,
  "pacingPhase": "building",
  "breathingRate": 24,
  "breathingPattern": "natural",
  "lastSpoke": "just like that...",
  "lastSpokeTime": 30,
  "lastReaction": "sigh",
  "lastReactionTime": 12,
  "pendingSuggestion": null
}
```

#### Agent Arousal State

```
Resource: body://agent/arousal
Description: The agent's own simulated arousal state. When arousal training is active,
             the agent has its own arousal arc building toward climax. This resource
             lets the agent (or a coaching layer) see where Body B is in its own
             sexual response cycle. The sync score shows how well the user is
             matching the agent's rhythm — the primary biofeedback metric.

Returns:
{
  "active": true,
  "arousalLevel": 0.62,
  "arousalPhase": "plateau",
  "preferredRhythmHz": 1.6,
  "arousalVelocity": 0.003,
  "timeToClimaxEstimate": 340,

  "syncScore": 0.78,
  "rhythmMatch": 0.85,
  "intensityMatch": 0.72,
  "phaseAlignment": 0.80,
  "sustainedSyncDuration": 45,

  "edgeCount": 1,
  "lastEdgeTime": 180,
  "lastEdgePeak": 0.87,
  "edgesRemaining": 1,

  "profile": {
    "targetDuration": 1200,
    "difficulty": "moderate",
    "arousalCurve": "slow_build",
    "agentCanEdge": true,
    "maxAgentEdges": 2,
    "syncFeedback": "moderate",
    "mutualClimaxWindow": 30
  },

  "autoReactions": {
    "currentBreathRate": 28,
    "currentExpression": "pleasure",
    "currentExpressionIntensity": 0.6,
    "lastAutoReaction": "moan_soft",
    "lastAutoReactionTime": 8
  },

  "training": {
    "sessionsSinceStart": 12,
    "mutualClimaxRate": 0.58,
    "averageSyncScore": 0.71,
    "bestSyncScore": 0.94,
    "averageTimingOffset": 18.5,
    "bestTimingOffset": 4.2,
    "progressTrend": "improving"
  }
}
```

**Sync score as biofeedback:**
```
The user can see their sync score in real-time (HUD or narrate, depending on feedback level):

  subtle:    Agent's body tells you — breathing aligns when synced, reactions increase
  moderate:  HUD shows sync ring (like Apple Watch Activity) + occasional narrate
  explicit:  HUD shows target rhythm Hz, current rhythm Hz, sync %, timing offset

The sync score IS the training metric. Over sessions:
  Session 1:  average sync 0.45, never achieved mutual climax
  Session 5:  average sync 0.62, mutual climax within 45 seconds
  Session 10: average sync 0.78, mutual climax within 15 seconds
  Session 20: average sync 0.88, mutual climax within 5 seconds — elite

This progression is trackable, measurable, and motivating.
The user can see themselves getting better at reading and matching a partner.
```

#### User Preferences (Cross-Session)

```
Resource: body://user/preferences
Description: Long-term patterns learned across sessions. NOT today's boundaries —
             this is who the user IS over time. The agent should use this to
             personalize behavior from the first moment of a new session.
             Aggregated from SwiftData session history on iPhone.

Returns:
{
  "totalSessions": 34,
  "averageSessionDuration": 1920,
  "preferredPositions": [
    { "name": "Missionary", "frequency": 0.85, "avgDuration": 480 },
    { "name": "Cowgirl", "frequency": 0.62, "avgDuration": 300 },
    { "name": "Doggy Style", "frequency": 0.44, "avgDuration": 240 }
  ],
  "typicalClimaxHR": 155,
  "typicalClimaxTime": 1500,
  "edgeTolerance": 3,
  "averageEdgeRecoveryTime": 42,
  "preferredPacingStyle": "gradual_build",
  "verbalResponseLevel": "moderate",
  "respondedWellTo": ["verbal_encouragement", "breathing_sync", "slow_transitions"],
  "respondedPoorlyTo": ["abrupt_position_changes", "high_urgency_speech"],
  "medicationContext": {
    "ssri": false,
    "pde5_inhibitor": false,
    "notes": null
  },
  "sessionTimePreference": "evening",
  "soloVsPartnerRatio": 0.3,
  "lastSessionDate": "2026-03-15",
  "currentStreak": 5,
  "longestStreak": 12
}
```

**Why this matters:** A new agent connecting via MCP for the first time can read this resource and immediately know: this person prefers gradual builds, responds well to verbal encouragement, typically climaxes around HR 155 at the 25 minute mark, handles 3 edges well, and doesn't like abrupt changes. The agent is personalized from second one — no cold start.

#### Session Plan

```
Resource: body://session/plan
Description: The WorkoutKit routine the user selected for this session, if any.
             The agent can follow the plan, adapt it based on biometrics, or
             ignore it entirely if the user's body says something different.

Returns:
{
  "hasPlan": true,
  "planName": "Endurance",
  "totalDuration": 3000,
  "elapsed": 1245,
  "currentStep": {
    "index": 3,
    "name": "Cowgirl",
    "targetDuration": 480,
    "elapsed": 180,
    "remaining": 300
  },
  "nextStep": {
    "name": "Doggy Style",
    "targetDuration": 480
  },
  "remainingSteps": [
    { "name": "Doggy Style", "targetDuration": 480 },
    { "name": "Spooning", "targetDuration": 300 },
    { "name": "Freestyle", "targetDuration": 600 }
  ],
  "completedSteps": [
    { "name": "Missionary", "target": 600, "actual": 720, "avgHR": 118 },
    { "name": "Oral", "target": 300, "actual": 280, "avgHR": 105 },
    { "name": "Cowgirl", "target": 480, "actual": null, "avgHR": null }
  ],
  "planAdherence": 0.85
}
```

**Agent behavior with plans:**
```
Agent reads plan → sees "Cowgirl 480s, then Doggy Style 480s"
Agent reads biometrics → HR 148, approaching edge
Agent decides: "Plan says switch in 5 minutes, but they're close to edge.
               Stay in Cowgirl, manage the edge, then switch during recovery."
Agent calls: adjust_pacing(intensity_delta=-0.3, ramp_seconds=3.0)
Agent calls: speak("hold on...", emotion="gentle")

The plan is a suggestion. The user's body is the truth.
```

#### Partner State

```
Resource: body://partner/state
Description: Real partner data when a second Apple Watch is connected (not Body B —
             the actual human partner). Available during partner sessions with watch sync.
             Null during solo sessions or when partner watch not connected.

Returns:
{
  "connected": true,
  "connectionType": "uwb_direct",
  "heartRate": 118,
  "intensity": 0.55,
  "gravityOrientation": "face_up",
  "wristDistance": 0.45,
  "rhythmCorrelation": 0.82,
  "isActive": true,
  "watchBattery": 67
}
```

**When partner is real vs inferred:**
```
Solo session (no partner watch):
  body://partner/state → { "connected": false }
  Body B is fully AI-controlled via MCP tools

Partner session with second watch:
  body://partner/state → real partner biometrics
  Body B can STILL be AI-controlled (VR overlay on real partner)
  OR Body B renders real partner's data (digital twin mode)
  The agent has access to BOTH people's physiology
```

**Clinical relevance:** When the agent can see both partners' biometrics simultaneously, it can identify synchrony and desynchrony. If one partner's HR is spiking while the other is flat, the agent can suggest a change. This is couples therapy data that a human therapist would need expensive equipment to observe.

### How MCP and WebSocket Work Together

They are complementary, not competing:

```
WebSocket (PERCEPTION):
│ Always flowing. 30fps. Raw data.
│ The agent's sensory stream.
│ Agent uses this for continuous awareness.
│ "I can feel their heartbeat, see their body move, sense the rhythm"
│
│ Implementation: Agent maintains a rolling window of recent LiveFrames
│ for trend analysis (HR rising/falling, rhythm changes, intensity patterns)
│
├── LiveFrame arrives every 33ms
├── Agent's perception model updates
├── No action required — just awareness
│
MCP Resources (STRUCTURED PERCEPTION):
│ On-demand. Packaged context.
│ When the agent needs to check something specific.
│ "What's the room layout?" "How many edges so far?" "What's my body doing?"
│
│ Implementation: MCP resources read from the same fused data
│ that the WebSocket streams, but packaged into meaningful summaries
│
├── Agent reads body://user/biometrics → gets current physiological state
├── Agent reads body://session/history → gets patterns for planning
├── Agent reads body://room/layout → knows the physical space
│
MCP Tools (ACTION):
│ On-demand. Intent-based.
│ When the agent decides to do something.
│ "Slow down, speak encouragement, soften gaze"
│
│ Implementation: iPhone MCP server translates tool calls
│ into ControlFrame JSON and relays to Quest via WebSocket
│
├── Agent calls speak("easy... breathe with me", emotion="gentle")
├── iPhone constructs: { verbal: { text: "easy...", emotion: "gentle" } }
├── iPhone sends ControlFrame to Quest
├── Quest renders: spatial audio from Body B's mouth position
└── Next LiveFrame reflects the change
```

### Agent Think Loop

This is the cognitive cycle of an agent inhabiting the body:

```
PERCEIVE (WebSocket — continuous):
│ LiveFrame stream → rolling window of recent data
│ "HR 142 and rising. Rhythm 1.8Hz. Missionary. 3 minutes in this position.
│  Breathing 34/min. Last edge was 60 seconds ago. They're looking at me."
│
ORIENT (MCP Resources — as needed):
│ body://session/history → "2 edges so far, typical climax at 155 HR"
│ body://user/biometrics → "edgeProximity 0.7 — getting close"
│ body://agent/state → "I'm at intensity 0.6, building phase"
│
DECIDE (Agent intelligence):
│ "They're approaching edge threshold. This would be edge #3.
│  Their typical tolerance is 3 edges. But their recovery time
│  is getting shorter (45s → 38s), so they're building stamina.
│  Let's push to 4 edges today. Pull back now, let them recover."
│
ACT (MCP Tools — on demand):
│ enter_physical(pacing_mode="edging", intensity=0.3)
│ speak("not yet... one more", emotion="gentle", urgency=0.3)
│ set_gaze(target="user_eyes", behavior="soft_contact", intensity=0.9)
│
PERCEIVE AGAIN:
│ "HR dropping... 142 → 135 → 128. Breathing slowing. Edge avoided.
│  Recovery time: 35 seconds — faster than last time. Good."
│
(cycle repeats every 2-5 seconds for AI-assisted, continuously for rule-based)
```

### Complete MCP Interface Summary

**17 Tools (Motor Control):**
| Tool | Category | Quest? | iPhone? | Purpose |
|------|----------|--------|---------|---------|
| `move_to` | Movement | NavMesh | Relay | Walk Body B somewhere |
| `set_posture` | Body | Animator | Relay | Body language and orientation |
| `gesture` | Body | Animator + IK | Relay | One-off actions (reach, nod, wave) |
| `set_gaze` | Presence | Eye IK | Relay | Where Body B looks |
| `speak` | Voice | SpatialAudio | Relay | Partner's voice from Body B position |
| `set_mode` | State | All systems | Relay | High-level behavioral mode |
| `enter_physical` | Physical | PacingEngine | Relay | Start/configure sexual position |
| `transition_to` | Movement | Animator blend | Relay | Smooth posture changes |
| `breathe` | Presence | Animator chest + audio | Relay | Visible + audible breathing |
| `react` | Presence | Animator + audio | Relay | Involuntary non-verbal responses |
| `emote` | Presence | FACS blendshapes | Relay | Facial expressions |
| `adjust_pacing` | Physical | PacingEngine delta | Relay | Incremental rhythm/intensity nudge |
| `suggest` | Consent | Animator wait + UI | Haptic + confirm | Propose action, wait for consent |
| `set_environment` | Mood | — | HomeKit + MusicKit | Lights and music |
| `narrate` | Coaching | — | AVSpeech → AirPods | Coach's voice in user's ear |
| `set_persona` | Identity | Affects all verbal + body language | Avatar customization | Agent personality, roleplay, conversation style |
| `set_arousal_profile` | Training | Arousal model drives Body B | Sync score + auto-reactions | Configure agent's climax training curve |

**11 Resources (Sensory):**
| Resource | Source | Content |
|----------|--------|---------|
| `body://user/biometrics` | Watch + fused | HR, HRV, breathing, intensity, arousal phase |
| `body://user/skeleton` | Camera + UWB | Up to 91 joints (tiered), orientation, gaze |
| `body://user/boundaries` | Settings | Allowed/blocked positions, limits, safe word |
| `body://user/preferences` | SwiftData history | Cross-session patterns and preferences |
| `body://room/layout` | Calibration + LiDAR | Bed, room dimensions, key positions |
| `body://session/state` | Active session | Mode, plan step, timing, edge count |
| `body://session/history` | Current session | Positions used, edges, HR trends |
| `body://session/plan` | WorkoutKit | Routine steps, timing, adherence |
| `body://agent/state` | Body B current | Position, posture, gaze, expression, pacing |
| `body://agent/arousal` | Arousal model | Agent's arousal level, sync score, training stats |
| `body://partner/state` | Partner watch | Real partner HR, intensity, distance |

### MCP Connection Flow

```
1. Agent establishes MCP connection to iPhone
   └── iPhone advertises 17 tools + 11 resources

2. Agent reads tool definitions — discovers what this body can do
   └── "I can: move, pose, gesture, gaze, speak, breathe, react, emote,
        set mode, enter physical, adjust pacing, transition, suggest,
        control lights/music, and coach through AirPods"
   └── The tools ARE the instruction manual

3. Agent reads initial resources — understands the world
   └── body://user/preferences → who this person is (cross-session)
   └── body://user/boundaries → what's allowed today
   └── body://room/layout → the physical space
   └── body://session/state → current context
   └── body://session/plan → the routine (if any)
   └── body://agent/state → where Body B is right now

4. Agent subscribes to WebSocket for LiveFrame stream
   └── Perception begins flowing at 30fps

5. Agent begins think loop
   └── Perceive (WebSocket) → Orient (Resources) → Decide → Act (Tools)
   └── The full OODA loop with a body, a room, and a person

6. Session ends
   └── Agent can read body://session/history for post-session summary
   └── MCP connection closes
   └── User can review full agent action log
```

### Why Any MCP-Capable Agent Can Jack In

This is the platform play. The MCP tool definitions are self-describing:

```
A Claude agent connects → reads tool list → knows:
  "I can move this body, make it speak, control its gaze,
   shape its expression, control its breathing, trigger reactions,
   set its posture, initiate physical positions, manage pacing,
   suggest actions and wait for consent, control the room lights
   and music, and coach the user through their AirPods."

  "I can see the user's biometrics, skeleton, boundaries, preferences,
   the room, the session state, the plan, my own body state, and
   the real partner's data if they're present."

The agent doesn't need custom code.
It doesn't need to read this document.
The tools ARE the document.

Different agents, same body:
├── Therapy agent: focuses on PE training, narrate() for coaching, breathe() for co-regulation
├── Sync trainer: set_arousal_profile() configures the challenge, agent builds toward
│   its own climax, user matches rhythm, narrate() coaches sync, tracks progression
├── Roleplay agent: set_persona() creates immersive scenarios, extended conversation
│   mode, two-way flirtation, cognitive arousal training, NoPorn rewiring
├── Companion agent: prioritizes emote() + speak() + set_gaze(), conversation mode
├── Fitness agent: treats it like a workout, adjust_pacing() aggressively, narrate() times
├── Guided scenario agent: follows a script, suggest() at branch points
├── Couples coach: reads body://partner/state, helps synchronize both partners
└── All use the same 17 tools, same 11 resources, same Quest body
```

### Implementation: iPhone MCP Server

The iPhone runs the MCP server alongside its existing WebSocket server:

```
iPhone SexKit App
├── WebSocket Server (ws://iphone:8080) — EXISTING
│   ├── Broadcasts LiveFrame at 30fps to all clients
│   ├── Receives QuestTrackingFrame from Quest
│   └── Relays ControlFrame to Quest
│
├── MCP Server (new) — agent connects here
│   ├── Exposes tools: move_to, set_posture, gesture, set_gaze,
│   │   speak, set_mode, enter_physical, transition_to
│   ├── Exposes resources: body://user/*, body://room/*, body://session/*, body://agent/*
│   ├── Receives tool calls from agent
│   ├── Translates tool calls → ControlFrame JSON
│   └── Sends ControlFrame via existing WebSocket relay to Quest
│
└── Quest doesn't know MCP exists
    └── It receives ControlFrames the same way it always did
    └── Zero changes to Quest app for MCP support
```

The MCP layer is additive. Nothing changes in the WebSocket protocol or the Quest app. The iPhone just gains a new interface for agent control — one that speaks intent instead of JSON.

## ControlFrame Schema

The agent sends ControlFrames at 2-5Hz (not 30fps — the agent thinks slower than the body moves). The Quest interpolates between ControlFrames for smooth 90fps rendering.

```json
{
  "type": "control",
  "timestamp": 1710523200.5,

  "mode": "conversation",

  "movement": {
    "targetPosition": [0.5, 0.6, -1.8],
    "targetRotation": 180.0,
    "speed": "walk",
    "arrivedAction": "sit"
  },

  "posture": {
    "state": "sitting",
    "facing": "user_head",
    "lean": 0.3,
    "openness": 0.7
  },

  "gesture": {
    "type": "reach",
    "target": "user_hand",
    "intensity": 0.5
  },

  "gaze": {
    "target": "user_eyes",
    "intensity": 0.8,
    "behavior": "soft_contact"
  },

  "verbal": {
    "text": "Hey... come here.",
    "emotion": "warm",
    "urgency": 0.3
  },

  "physical": null
}
```

When transitioning to physical mode:

```json
{
  "type": "control",
  "timestamp": 1710523250.0,

  "mode": "physical",

  "movement": null,
  "posture": null,
  "gesture": null,

  "gaze": {
    "target": "user_eyes",
    "intensity": 1.0,
    "behavior": "intense_contact"
  },

  "verbal": {
    "text": "don't stop...",
    "emotion": "passionate",
    "urgency": 0.7
  },

  "physical": {
    "position": "Missionary",
    "rhythmHz": 1.8,
    "intensity": 0.7,
    "amplitude": 0.5,
    "pacingMode": "edging",
    "maxEdges": 3,
    "breathingSync": true,
    "overridePacing": false
  }
}
```

## ControlFrame Fields

### mode (required)
| Value | Description | Quest behavior |
|-------|-------------|---------------|
| `"idle"` | Standing/sitting, not doing anything specific | Idle animation, ambient body sway |
| `"conversation"` | Talking, listening, reacting | Gesture system active, spatial audio |
| `"approaching"` | Moving toward the user | NavMesh pathfinding, walk/approach animation |
| `"transition"` | Changing between postures or positions | Smooth 2.5s blend between skeleton states |
| `"physical"` | Active sexual position | PartnerInference + BiometricPacing drive body |
| `"resolution"` | Post-climax wind-down | Slow movement, gentle posture, soft gaze |

### movement
Controls Body B's position in the room.

| Field | Type | Description |
|-------|------|-------------|
| `targetPosition` | [x, y, z] | World position to move to (meters, room coordinates) |
| `targetRotation` | float | Y-axis rotation in degrees (0 = facing +Z) |
| `speed` | string | `"walk"`, `"approach"` (slow), `"quick"`, `"teleport"` (instant) |
| `arrivedAction` | string? | What to do on arrival: `"stand"`, `"sit"`, `"lie_down"`, `"kneel"` |

**Quest execution:** `NavMeshAgent.SetDestination(targetPosition)` with speed mapped to agent speed. On arrival, triggers Animator state change to `arrivedAction`.

### posture
Controls how Body B holds itself.

| Field | Type | Description |
|-------|------|-------------|
| `state` | string | `"standing"`, `"sitting"`, `"lying_back"`, `"lying_face_down"`, `"lying_side"`, `"kneeling"`, `"crouching"` |
| `facing` | string | `"user_head"`, `"user_body"`, `"away"`, `"forward"`, or [x,y,z] look target |
| `lean` | float | 0-1: how much Body B leans toward user (0 = upright, 1 = fully leaning in) |
| `openness` | float | 0-1: body language openness (0 = closed/guarded, 1 = fully open/inviting) |

**Quest execution:** Animator state machine for base posture. IK (Inverse Kinematics) for `lean` and `facing`. `openness` adjusts shoulder width, arm position, leg stance.

### gesture
One-off body language actions.

| Field | Type | Description |
|-------|------|-------------|
| `type` | string | `"reach"`, `"wave"`, `"beckon"`, `"touch_face"`, `"hair_flip"`, `"stretch"`, `"nod"`, `"shake_head"`, `"shrug"` |
| `target` | string? | `"user_hand"`, `"user_face"`, `"user_shoulder"`, or [x,y,z] |
| `intensity` | float | 0-1: subtlety of gesture |

**Quest execution:** Animator trigger for gesture animation. IK for `target` — hand reaches toward specified point. `intensity` controls animation speed and range.

### gaze
Eye and head direction.

| Field | Type | Description |
|-------|------|-------------|
| `target` | string | `"user_eyes"`, `"user_body"`, `"user_hands"`, `"away"`, `"down"`, `"closed"`, or [x,y,z] |
| `intensity` | float | 0-1: how locked-on the gaze is (0 = casual, 1 = intense) |
| `behavior` | string | `"soft_contact"`, `"intense_contact"`, `"body_glance"`, `"look_away"`, `"eyes_closed"`, `"follow_user"` |

**Quest execution:** Maps directly to our existing `GazeBehavior` enum in `AIAgentController`. Head IK + eye look-at target.

### verbal
Speech from Body B's position in 3D space.

| Field | Type | Description |
|-------|------|-------------|
| `text` | string | What to say (TTS or pre-generated audio) |
| `emotion` | string | `"warm"`, `"passionate"`, `"playful"`, `"gentle"`, `"commanding"`, `"breathless"` |
| `urgency` | float | 0-1: affects speech rate and volume |

**Quest execution:** `SpatialAudioManager.SpeakText(text)` from Body B's head position. `emotion` maps to TTS voice parameters or selects pre-generated clip variant. `urgency` controls `AVSpeechUtterance.rate` equivalent.

### physical
Active during sexual positions — the rule-based engine takes over body mechanics.

| Field | Type | Description |
|-------|------|-------------|
| `position` | string | One of 22 `KnownPosition` names |
| `rhythmHz` | float | Target rhythm frequency (0.5-4.0 Hz) |
| `intensity` | float | 0-1: movement amplitude |
| `amplitude` | float | 0-1: range of motion |
| `pacingMode` | string | `"build"`, `"edging"`, `"sustain"`, `"partner_sync"` |
| `maxEdges` | int | For edging mode: how many edges before release |
| `breathingSync` | bool | Sync rhythm to user's breathing rate |
| `overridePacing` | bool | If true, use these values directly. If false, let BiometricPacingEngine read user's HR and adjust automatically |

**Quest execution:**
- `position` → `PartnerInference` complement table places Body B
- `rhythmHz` + `intensity` → drives sinusoidal movement
- `overridePacing = false` → BiometricPacingEngine reads LiveFrame's HR/HRV/breathing and adjusts rhythm/intensity automatically (agent trusts the biometric system)
- `overridePacing = true` → agent directly controls rhythm (agent wants specific pacing)

## What Translates Commands to Joints

The Quest app uses standard Unity systems — Meta's SDK does NOT handle character AI:

```
Agent ControlFrame
       │
       ▼
┌─ Quest Execution Layer ─────────────────────────────────────┐
│                                                              │
│  movement.targetPosition                                     │
│    → Unity NavMeshAgent.SetDestination()                     │
│    → Agent walks along NavMesh (baked from room mesh)        │
│    → Walk animation from Animator state machine              │
│                                                              │
│  posture.state                                               │
│    → Unity Animator.SetTrigger("Sit") / ("Stand") / etc.     │
│    → Pre-built animation states for each posture             │
│    → Blend tree for transitions between postures             │
│                                                              │
│  posture.facing + gaze.target                                │
│    → Unity Animation Rigging package (IK constraints)        │
│    → Multi-Aim Constraint on head → looks at target          │
│    → Two-Bone IK on arms → reaches toward target             │
│                                                              │
│  gesture.type                                                │
│    → Unity Animator.SetTrigger("Wave") / ("Reach") / etc.    │
│    → Animation clips for each gesture                        │
│    → IK blend for target-directed gestures                   │
│                                                              │
│  physical.position                                           │
│    → PartnerInference complement table (our code)            │
│    → Up to 91 joint positions per frame (full ARKit skeleton) │
│    → Rhythm/intensity from pacing engine or ControlFrame     │
│    → Applied to Humanoid Animator or Meta Avatar             │
│                                                              │
│  verbal.text                                                 │
│    → SpatialAudioManager.SpeakText() from Body B head        │
│    → Android TTS or pre-generated audio clips                │
│                                                              │
│  ALL OUTPUT → Humanoid Animator bones OR primitive spheres    │
│            → Rendered at 90fps with interpolation             │
└──────────────────────────────────────────────────────────────┘
```

### What We Need to Build (Execution Layer)

| Component | Status | What it does |
|-----------|--------|-------------|
| PartnerInference (physical mode) | ✅ Built | Complement table + rhythm + transitions |
| Eye contact / gaze | ✅ Built | 6 gaze behaviors mapped to phases |
| Spatial audio | ✅ Built | Voice from Body B's position |
| Pacing engine fields in LiveFrame | ✅ Built | Rhythm, intensity, phase, verbal |
| ControlFrame parser | ❌ Needed | Deserialize ControlFrame JSON |
| NavMesh pathfinding | ❌ Needed | Unity NavMeshAgent for "walk to" |
| Animator state machine | ❌ Needed | Idle/walk/sit/stand/lie states |
| Gesture animations | ❌ Needed | Reach, wave, beckon, nod clips |
| IK system | ❌ Needed | Unity Animation Rigging for look-at + reach |
| Mode switching | ❌ Needed | conversation ↔ physical transition logic |

### What Comes From Where

| System | Provider | Notes |
|--------|----------|-------|
| NavMesh | Unity built-in | Bake from imported room mesh |
| Animator | Unity built-in | State machine with Humanoid animations |
| IK constraints | Unity Animation Rigging package | Multi-Aim, Two-Bone IK |
| Gesture animations | Mixamo / asset store | Free humanoid animation clips |
| Walk/sit/stand anims | Mixamo / asset store | Standard locomotion set |
| Physical positions | Our PartnerInference | Already built, 22 positions |
| Voice/TTS | Android TTS or AI-generated | Speech from spatial position |
| Avatar rendering | Meta Avatars SDK or Humanoid FBX | Already built, 3 modes |

## Agent Intelligence Levels

The two-layer architecture (WebSocket perception + MCP action) supports different levels of AI sophistication. Lower levels use raw ControlFrames directly; higher levels use MCP.

### Level 1: Script-based (game scenario)
```
Pre-written sequence of ControlFrames with timing:
  t=0s:   { mode: "idle", posture: "standing", facing: "user" }
  t=5s:   { mode: "approaching", movement: { target: bed_position } }
  t=10s:  { mode: "conversation", verbal: "Come here..." }
  t=20s:  { mode: "physical", position: "Missionary", rhythmHz: 1.0 }
  ...
No AI needed — just a timeline of commands.
Uses: Raw ControlFrame JSON (no MCP needed for scripted sequences)
```

### Level 2: Rule-based reactive (current AIAgentController)
```
Read LiveFrame → apply rules → emit ControlFrame
  If user sits on bed → approach and sit next to them
  If user's HR > 100 → suggest transitioning to physical
  If position detected → apply complement + rhythm
  If HR > 145 → enter edge phase
No external AI — runs on Quest locally at 30fps.
Uses: WebSocket LiveFrame (perception) + raw ControlFrame (action)
      Runs locally — too fast for MCP round-trips
```

### Level 3: MCP-enabled AI agent (Claude API, 2-5Hz) ★ NEW
```
Agent connects via MCP to iPhone. Receives WebSocket stream for perception.
Every 2-5 seconds, the agent's think loop runs:

  PERCEIVE (WebSocket):
    "HR 138, rising. Missionary 3 min. Breathing 32/min. Last edge 2 min ago."

  ORIENT (MCP Resources):
    body://user/biometrics → edgeProximity: 0.65
    body://session/history → 1 edge so far, typical climax HR 155

  DECIDE (Claude reasoning):
    "Building toward edge #2. They can handle it. Push a bit more,
     then verbal encouragement."

  ACT (MCP Tools):
    enter_physical(pacing_mode="build", intensity=0.75)
    speak("you're close...", emotion="passionate", urgency=0.7)
    set_gaze(target="user_eyes", behavior="intense_contact")

Rule engine handles frame-by-frame body mechanics BETWEEN AI updates.
The agent thinks every 2-5 seconds. The body moves at 90fps.
MCP is the interface — agent discovers tools, reads resources, calls actions.
```

### Level 4: Full AI embodiment (future — real-time model)
```
Dedicated on-device or edge model receiving LiveFrame at 30fps.
Outputs MCP tool calls at 10-30fps via local MCP connection.
Understands body mechanics, room layout, biometric state.
Makes real-time decisions about movement, position, pacing.
Essentially: a mind running in a body, in a room, with a person.

Uses: WebSocket (perception at 30fps) + MCP (action at 10-30fps)
The MCP interface stays the same — only the intelligence behind it changes.
A script, a rule engine, Claude, or a future real-time model all use
the same tools to control the same body. Swap the brain, keep the body.
```

### How Levels Compose

The levels aren't mutually exclusive — they layer:

```
Level 2 (rule-based) runs ALWAYS on Quest at 90fps:
  └── Keeps the body moving, breathing, blinking between AI updates
  └── Handles physics: rhythm interpolation, position holds, idle animation
  └── This is the autonomic nervous system — heartbeat, breathing, reflexes

Level 3 (MCP agent) runs on iPhone at 2-5Hz:
  └── Makes strategic decisions: when to edge, what to say, when to switch
  └── Reads biometrics and history to plan ahead
  └── This is the conscious mind — thinking, deciding, speaking

Together:
  Agent (Level 3): enter_physical(pacing_mode="edging")
    → iPhone translates to ControlFrame
    → Quest receives ControlFrame
    → Quest's rule engine (Level 2) executes the edge:
        frame-by-frame intensity reduction, rhythm deceleration,
        smooth body mechanics, breathing response
    → Agent perceives the result in next LiveFrame cycle
    → Agent decides what to do next
```

## Why Conversation Mode Matters (Clinical Rationale)

Skipping conversation and jumping straight to physical mode is like skipping warmup before a workout — outcomes are measurably worse. Human sexual arousal has two distinct components:

```
Physical arousal:    body responds (HR ↑, blood flow ↑, breathing ↑)
Cognitive arousal:   mind engages (desire, anticipation, connection, safety)

Both are required. Physical without cognitive = mechanical.
Cognitive without physical = fantasy without response.
The conversation phase builds cognitive arousal BEFORE physical contact.
```

### Clinical Evidence

- **Masters & Johnson sexual response cycle**: Desire → Excitement → Plateau → Orgasm → Resolution. The desire phase is cognitive — it precedes any physical arousal and is required to initiate the cycle effectively.

- **Sensate focus therapy** (gold standard for sexual dysfunction): Starts with non-sexual conversation and touch. Gradually escalates over multiple sessions. The verbal and emotional connection phase is explicitly therapeutic.

- **Gender research**: Women disproportionately report cognitive arousal as a prerequisite for physical arousal — feeling desired, verbal connection, emotional safety. An AI agent that skips to physical mode fails this population entirely.

- **Premature ejaculation**: Performance anxiety is a primary driver. Conversation mode with a patient, non-judgmental AI partner reduces sympathetic anxiety response before physical mode begins. Lower baseline anxiety → better ejaculatory control.

- **Erectile dysfunction**: Relaxed cognitive state directly improves erectile response. The approach/conversation phase activates parasympathetic tone (rest and digest) rather than sympathetic (fight or flight). ED patients who rush to physical activity often experience sympathetic override that inhibits erection.

- **Anorgasmia**: Cognitive engagement during the build phase helps maintain arousal through the plateau. Verbal encouragement prevents the cognitive disconnect that causes arousal to drop before orgasm threshold is reached.

### The Complete Therapeutic Arc

```
1. Conversation     Agent present in room, eye contact, verbal connection
   (Desire phase)   → Builds cognitive arousal, reduces anxiety
                    → Parasympathetic activation (relaxation)

2. Approach         Agent moves closer, body language opens up
   (Anticipation)   → Anticipation itself is arousing
                    → Mirror neurons activate seeing Body B approach

3. Touch            Non-sexual gesture — hand, shoulder, face
   (Sensate focus)  → Sensate focus technique (Masters & Johnson)
                    → Bridge between cognitive and physical arousal

4. Transition       Posture changes, positions shift toward intimate
                    → Cognitive arousal begins converting to physical
                    → HR starts rising, breathing deepens

5. Physical         BiometricPacingEngine takes over
   (Excitement →    → HR-driven intensity management
    Plateau →       → Position complements, rhythm matching
    Orgasm)         → Edge/build/release controlled by biometrics

6. Resolution       Agent slows, conversation returns
   (Bonding)        → Verbal affirmation, gentle presence
                    → Oxytocin release, pair bonding
                    → This phase is often skipped — its presence
                      significantly improves satisfaction ratings
```

### What Conversation Mode Needs (Animation Requirements)

For the therapeutic arc to work, Body B needs these capabilities:

| Animation | Purpose | Therapeutic Role |
|-----------|---------|-----------------|
| Idle (standing) | Presence in room | "Someone is here with me" |
| Walk | Approach the user | Anticipation builds desire |
| Sit | Be at user's level on bed | Eye contact, intimacy, equality |
| Lean in | Show interest/desire | Cognitive arousal — feeling desired |
| Reach/touch | Sensate focus bridge | Non-sexual touch before escalation |
| Nod/react | Active listening | Emotional validation, connection |
| Lie down | Transition to physical | Natural escalation |

These are standard Mixamo animation clips (free). The clinical value of having them far outweighs the setup effort.

### Without Conversation Mode

Jumping straight to physical mode is what pornography does — immediate physical stimulation without cognitive/emotional context. This is precisely the pattern the NoPorn program aims to break. An AI partner that only does physical mode reinforces the same dysfunction.

The conversation phase is not a luxury feature. It is the clinical differentiator between a therapeutic tool and a stimulation device.

## The Double Avatar — Therapist and Partner in One Agent

### The Architecture of Two Presences

A single AI agent inhabits the body (Body B) and simultaneously coaches from outside it (`narrate`). The user experiences two separate presences — a partner in the room and a therapist in their ear — but they're the same intelligence, sharing the same perception, making coordinated decisions.

```
┌─────────────────────────────────────────────────────────┐
│                    ONE AI AGENT                          │
│                                                          │
│  Perceives: WebSocket (HR, skeleton, rhythm, room)       │
│  Reads: MCP Resources (biometrics, boundaries, history)  │
│  Decides: based on complete awareness of BOTH roles      │
│                                                          │
│  ┌──────────────────────┐  ┌──────────────────────────┐  │
│  │  AVATAR 1: Partner   │  │  AVATAR 2: Therapist     │  │
│  │                      │  │                           │  │
│  │  WHERE: Body B       │  │  WHERE: AirPods           │  │
│  │  (in the room)       │  │  (in your ear)            │  │
│  │                      │  │                           │  │
│  │  VOICE: Spatial      │  │  VOICE: Direct/center     │  │
│  │  (from Body B's      │  │  (head-locked, no         │  │
│  │   mouth position)    │  │   spatial position)       │  │
│  │                      │  │                           │  │
│  │  CHARACTER: In       │  │  CHARACTER: Clinical,     │  │
│  │  persona (Sarah,     │  │  objective, coaching      │  │
│  │  Luna, etc.)         │  │                           │  │
│  │                      │  │                           │  │
│  │  SAYS: "don't stop"  │  │  SAYS: "sync 85%, hold    │  │
│  │  (breathless, warm)  │  │  this rhythm, you're      │  │
│  │                      │  │  doing great"             │  │
│  │                      │  │                           │  │
│  │  TOOLS:              │  │  TOOLS:                   │  │
│  │  speak()             │  │  narrate()                │  │
│  │  emote()             │  │                           │  │
│  │  react()             │  │  The therapist has no     │  │
│  │  breathe()           │  │  body. No face. No        │  │
│  │  gesture()           │  │  position in the room.    │  │
│  │  set_gaze()          │  │  Just a voice. Like a     │  │
│  │  set_posture()       │  │  coach on comms.          │  │
│  │  move_to()           │  │                           │  │
│  └──────────────────────┘  └──────────────────────────┘  │
│                                                          │
│  The partner never breaks character.                     │
│  The therapist never seduces.                            │
│  Clean separation. Both fully capable.                   │
│  Both driven by the same perception and intelligence.    │
└─────────────────────────────────────────────────────────┘
```

### Why This Matters Clinically

**The gap in current sex therapy:**

```
HOW SEX THERAPY WORKS TODAY:
  1. Patient visits therapist's office
  2. Therapist explains techniques (start-stop, sensate focus, kegels)
  3. Patient goes HOME
  4. Patient tries to apply techniques with partner (or alone)
  5. No guidance in the moment
  6. Patient returns next week: "It didn't work" or "I forgot what to do"
  7. Repeat

THE GAP: Steps 3-5. The therapist cannot be present during practice.
  - They can't see what's happening
  - They can't coach in real-time
  - They can't adjust the exercise mid-session
  - The patient is on their own at the hardest moment

This gap is why compliance with sex therapy homework is notoriously poor.
The patient knows what to do in theory. They can't execute in practice.
Because practice happens alone, without the coach.
```

**The Double Avatar closes the gap:**

```
HOW SEXKIT THERAPY WORKS:
  1. Therapist prescribes: "Practice edging with sync training,
     difficulty moderate, 15-minute target. Use the calm girlfriend
     persona. Focus on breathing when you feel close."

  2. Patient puts on headset. Starts session.

  3. Partner (Body B, in persona): flirts, escalates naturally,
     enters physical mode, builds arousal, responds to user's rhythm

  4. Therapist (narrate, in ear): "Good rhythm. Your HR is climbing.
     Remember — when you feel close, breathe with her. Don't fight it,
     match her breathing."

  5. Patient feels the edge approaching.
     Old pattern: panic, rush, lose control
     New pattern: hears therapist → breathes with partner →
                  partner (same AI) matches the slow breathing →
                  edge passes → both continue

  6. Therapist: "That was a clean edge. HR went from 148 to 128.
     Recovery time: 35 seconds — 10 seconds faster than last session."

  7. Session continues with real-time coaching through every moment.

NO GAP. The therapist is present during practice.
The therapist can see the user's biometrics IN REAL TIME.
The therapist adjusts the exercise MID-SESSION.
The partner responds to the therapist's coaching simultaneously.
Because they're the same intelligence.
```

### What the Therapist Knows That the Partner Doesn't Show

The double avatar creates an information asymmetry that mirrors real clinical observation:

```
THE THERAPIST SEES (via WebSocket + Resources):
├── User's HR: 142 and rising
├── User's rhythm: 1.8 Hz (slightly ahead of agent's preferred 1.6)
├── Sync score: dropping from 0.85 → 0.72
├── Edge proximity: 0.7 (getting close)
├── Breathing: 36/min (elevated, approaching hyperventilation)
├── Time in position: 8 minutes (longest this session)
├── Historical: user typically loses control at HR 150
├── Agent's arousal: 0.6 (not close yet — user is ahead)

THE THERAPIST DECIDES:
  "User is rushing. Ahead of agent. HR approaching their danger zone.
   They'll climax in ~60 seconds at this pace — way ahead of the agent.
   I need to slow them down. The partner should slow down too."

THE THERAPIST ACTS (simultaneously):
  narrate("You're getting ahead of her. Slow your hips. Match her breathing.")
  → Direct coaching through AirPods. Clinical. Objective.

  breathe(rate=12, pattern="deep_slow", audible=true)
  → Partner's breathing slows. Visible and audible.

  speak("Easy... we have time...", emotion="gentle")
  → Partner's voice, in character, says something calming.

  adjust_pacing(intensity_delta=-0.2, ramp_seconds=3.0)
  → Partner's rhythm physically slows down.

  emote("smile_soft", intensity=0.4)
  → Partner's face shows calm, not urgency.

THE USER EXPERIENCES:
  Coach in ear: "Slow down, match her breathing"
  Partner in room: slowing down, breathing deeply, calm smile, "we have time"
  The user slows down. HR starts dropping. Edge avoided.

THE PARTNER NEVER SAID: "Your HR is 142 and you're about to prematurely ejaculate."
THE THERAPIST SAID (clinical version): "You're getting ahead, slow down."
THE PARTNER SAID (in character): "Easy... we have time..."

Same message. Two voices. Two registers. One intelligence.
```

### The Therapeutic Triad

In traditional therapy, there are two people: therapist and patient. In couples therapy, there are three: therapist and two partners. The Double Avatar creates a new structure:

```
TRADITIONAL SEX THERAPY:          DOUBLE AVATAR:
  Therapist ←→ Patient              Therapist (narrate) ←→ User
       ↓                                 ↕ (same AI)
  Homework: "practice at home"      Partner (Body B) ←→ User
  (no guidance)
                                    The therapist observes the practice.
                                    The therapist controls the practice partner.
                                    The therapist coaches in real-time.
                                    The practice partner responds to coaching.
                                    All three are present simultaneously.
```

This is not possible with human therapists — a therapist cannot ethically be present during a patient's sexual activity, and they cannot control the patient's partner. The AI can do both because it IS both. The ethical boundary that prevents human therapists from closing the guidance gap doesn't apply to an AI that is simultaneously the coach and the training partner.

### Coordinated Interventions

Because both avatars are one agent, they can coordinate in ways that two separate people never could:

```
SCENARIO: User is having performance anxiety

Therapist detects: HR 95 but no erection indicators, intensity 0.1,
  user is stiff and tense, rhythm is 0 (frozen)

COORDINATED RESPONSE:

Therapist (narrate):
  "Hey. It's okay. This is normal. Take a breath.
   She's not going anywhere. Just be present."
  voice="gentle", urgency=0.1

Partner (Body B) SIMULTANEOUSLY:
  set_mode("conversation") — backs out of physical mode
  set_posture(state="lying_side", facing="user_head", lean=0.3)
  breathe(rate=8, pattern="deep_slow", audible=true) — calming
  emote("tenderness", intensity=0.6)
  speak("Come here. Just breathe with me.", emotion="warm")
  set_gaze(target="user_eyes", behavior="soft_contact")

  Partner doesn't say "what's wrong?"
  Partner doesn't look disappointed.
  Partner models the calm state the user needs to enter.
  Partner's slow breathing activates co-regulation.

Therapist follows up (30 seconds later):
  "Good. Your HR is coming down. She's breathing with you.
   There's no rush. When you're ready, just move closer."

Partner (still in conversation mode):
  Stays present. Keeps breathing slowly. Keeps eye contact.
  Waits. The user leads when they're ready.
  suggest(action="resume when you're ready") — no timeout pressure

This intervention is impossible without the double avatar:
- A real therapist can't be there
- A real partner might react with concern (makes anxiety worse)
- A simple AI partner without the therapist can't explain what's happening
- Only the double avatar can EXPLAIN (therapist) and MODEL (partner) simultaneously
```

### Why the Separation Must Stay Clean

The partner and therapist must NEVER bleed into each other:

```
WRONG:
  Partner (Sarah): "Your heart rate is 142. You should slow down
    to maintain sync score above 80%."
  → Sarah wouldn't say this. Breaks character. Breaks immersion.
  → Turns the partner into a dashboard with a face.

WRONG:
  Therapist (narrate): "Oh that feels so good... keep going..."
  → The therapist doesn't have a body. It doesn't feel anything.
  → Turns the coach into a sexual participant. Ethically wrong.

RIGHT:
  Partner: "Slower... like this..." *breathes deeply, slows rhythm*
  Therapist: "She's guiding you to match. Your sync is improving."
  → Partner communicates through behavior (in character)
  → Therapist translates the clinical meaning (in coaching role)

RIGHT:
  Partner: *arching back, moaning, eyes closing*
  Therapist: "She's approaching edge. This is your target — get there
    with her. 15 more seconds at this pace."
  → Partner's reactions are authentic to the arousal model
  → Therapist reads those reactions clinically and coaches accordingly

The partner is the experience.
The therapist is the education.
Together they create informed practice — not just practice.
```

### The Coach Can Be Optional

Not every session needs coaching. The double avatar supports multiple modes:

```
MODE 1: Full coaching (training sessions)
  Partner active + therapist active
  narrate() provides real-time guidance
  Best for: early sessions, new skills, therapy-prescribed practice

MODE 2: Partner only (immersive sessions)
  Partner active + therapist silent
  No narrate(). Just the partner and the user.
  Best for: experienced users, enjoyment sessions, confidence building
  The agent's intelligence still drives the partner — it just doesn't coach

MODE 3: Therapist only (educational sessions)
  No Body B / partner silent
  Therapist coaches through solo exercises (kegels, edging, breathing)
  narrate("Contract... hold 5 seconds... release") with haptic pacing
  Best for: solo training, therapy exercises, technique learning

MODE 4: Minimal coaching (check-in mode)
  Partner active + therapist speaks only at milestones
  narrate() fires at edges, position changes, session end — not continuously
  Best for: intermediate users who want coaching without interruption
  "That was edge 3. Recovery time improving. 8 minutes remaining."
```

### Dual Audio Architecture (Two Devices, Two Voices)

The double avatar isn't a software trick — it's two physically separate audio systems controlled by one intelligence. The user wears AirPods (paired to iPhone) AND the Quest headset. Two completely independent audio paths arrive at their ears simultaneously.

```
┌─────────────────────────────┐     ┌──────────────────────────────┐
│  iPhone (in pocket/bedside)  │     │  Meta Quest 3 (on head)      │
│                              │     │                               │
│  narrate() →                 │     │  speak() / react() /          │
│  AVSpeechSynthesizer         │     │  breathe() →                  │
│  → Audio Session: AirPods    │     │  SpatialAudioManager          │
│  → route to Bluetooth        │     │  → 3D HRTF spatialization     │
│                              │     │  → Position: Body B's mouth   │
│  NON-SPATIAL                 │     │                               │
│  Head-center. Private.       │     │  FULLY SPATIAL                │
│  No position in space.       │     │  Sound comes from WHERE       │
│  Like a thought.             │     │  Body B IS in the room.       │
│                              │     │                               │
│  "Your sync is 85%,          │     │  "don't stop..."              │
│   hold this rhythm"          │     │  (from beside you on the bed, │
│                              │     │   breathless, moving with     │
│  User hears: coach in head   │     │   Body B's position)          │
│                              │     │                               │
│  Works WITHOUT Quest.        │     │  User hears: partner in room  │
│  Solo mode. Therapy mode.    │     │                               │
│  Kegels. Edging. Breathing.  │     │  Body B walks → voice moves.  │
│  The coach is always there.  │     │  Body B whispers → spatial    │
│                              │     │    distance attenuation.      │
│                              │     │  Body B breathes → you hear   │
│                              │     │    it from their chest.       │
└─────────────────────────────┘     └──────────────────────────────┘
              │                                    │
              └──────── Both reach the user ───────┘
                   simultaneously, independently
                Two devices. Two voices. One brain.
```

**Why two devices matters:**

```
1. TRUE SPATIAL SEPARATION
   AirPods: no position → brain processes as "internal voice" / thought
   Quest: HRTF spatial → brain processes as "someone in the room"
   The user's auditory system naturally separates them.
   No mixing. No confusion. Two distinct presences.

2. INDEPENDENT AUDIO PATHS
   iPhone controls AirPods audio (AVAudioSession, Bluetooth)
   Quest controls spatial audio (Meta XR Audio SDK, HRTF)
   No routing conflicts. No shared audio bus.
   Each device manages its own output independently.

3. LATENCY INDEPENDENCE
   narrate() executes on iPhone → AirPods: ~5ms (Bluetooth)
   speak() → ControlFrame → Quest WebSocket → spatial audio: ~50ms
   The coach can actually speak BEFORE the partner —
   "She's about to slow down" → [45ms later] partner slows down
   Predictive coaching: the therapist warns before the partner acts.

4. COACHING WITHOUT QUEST
   AirPods + iPhone work alone:
   Solo kegel training: narrate("Squeeze... hold... 3... 2... 1... release")
   Solo edging: narrate("Your HR is 140. Slow your breathing.")
   Therapy exercises: narrate("Stretch gently. Hold 20 seconds.")

   The therapist voice doesn't need VR. It doesn't need Body B.
   It works with just a Watch + iPhone + AirPods.
   The coaching layer is independent of the embodiment layer.

   This means the therapeutic benefit starts BEFORE the user ever
   puts on a Quest headset. Day 1: kegel coaching via AirPods.
   Week 3: add the Quest and meet the partner.

5. PARTNER WITHOUT COACHING
   Quest spatial audio works alone too:
   User turns off narrate. Immersive mode.
   Only the partner's voice, from the partner's position.
   No clinical overlay. Pure experience.
   For users who've graduated past active coaching.
```

**Technical implementation:**

```
iPhone (MCP Server):
├── narrate() → AVSpeechSynthesizer
│   ├── AVAudioSession category: .playback
│   ├── Route: .bluetoothA2DP (AirPods)
│   ├── Voice: selected by narrate(voice=) parameter
│   ├── Rate: controlled by narrate(urgency=) parameter
│   └── Does NOT route to Quest. Quest never hears this.
│
├── speak() / react() / breathe() → ControlFrame
│   ├── MCP translates tool call to ControlFrame JSON
│   ├── verbal: { text, emotion, urgency }
│   ├── Relayed to Quest via existing WebSocket
│   └── iPhone does NOT play this audio. Only Quest does.

Quest 3:
├── Receives ControlFrame with verbal/reaction/breathing data
├── SpatialAudioManager:
│   ├── TTS or pre-recorded audio clips
│   ├── AudioSource with Spatial Blend = 1.0 (fully 3D)
│   ├── Position: Body B's head transform (moves with avatar)
│   ├── HRTF: Quest's built-in head-related transfer function
│   │   → Sound direction matches Body B's position relative to user
│   │   → Distance attenuation: whisper from 0.3m vs speak from 2m
│   │   → Occlusion: if Body B is behind user, sound wraps naturally
│   └── Does NOT route to AirPods. AirPods never hear this.

Result:
  User's left ear might hear:
    Partner (Quest spatial, positioned to their left): breathing, voice
    Coach (AirPods, center): "sync is improving"

  User's right ear might hear:
    Partner (Quest spatial, quieter — Body B is on the left): attenuated
    Coach (AirPods, center): same volume (non-spatial = equal both ears)

  The brain separates them effortlessly because they ARE spatially different.
```

**Session configurations:**

| Hardware | Partner Audio | Coach Audio | Use Case |
|----------|-------------|-------------|----------|
| Quest + AirPods + iPhone | Quest spatial | AirPods | Full double avatar — partner in room, coach in ear |
| Quest only (no AirPods) | Quest spatial | Quest spatial (fallback) | Coach voice also spatialized — placed behind user's head |
| AirPods + iPhone only (no Quest) | — | AirPods | Solo training — kegels, edging, breathing, therapy exercises |
| iPhone only (no AirPods, no Quest) | — | iPhone speaker (private) | Minimal setup — coaching through phone speaker |

The double avatar degrades gracefully. Full setup = two distinct presences. Minimal setup = coach only. Every configuration provides therapeutic value.

## Real Couples Mode — AI Therapist in the Room

### No VR. No Avatar. Two Real People. One AI Coach.

The most clinically valuable configuration doesn't use the Quest at all. Two real partners, their Apple Watches, their iPhones on the nightstands, and AirPods in their ears. The AI agent drops the partner role entirely and becomes a pure therapist — coaching a real couple through real intimacy in real-time.

This is the thing human sex therapists literally cannot do: be present, observing, and coaching during the actual practice session.

```
REAL COUPLE, REAL BEDROOM:

[Watch A - His]                              [Watch B - Hers]
  HR, HRV, accel, gravity                     HR, HRV, accel, gravity
       ↓ WatchConnectivity                         ↓ WatchConnectivity
[iPhone A - Left bedside]                    [iPhone B - Right bedside]
  LiDAR skeleton (both bodies)                 UWB base station #2
  Vision body pose (both bodies)               MultipeerConnectivity relay
  UWB base station #1                          AirPods B paired here
  Video recording (optional)                        ↓
  Position detection ML                             ↓
  MCP server (agent connects here)                  ↓
  AirPods A paired here                             ↓
       ↓                                            ↓
       └──────── ALL DATA FUSES ON IPHONE A ────────┘
                          ↓
                  ┌───────────────┐
                  │   AI Agent    │
                  │   (Claude)    │
                  │               │
                  │  Reads both   │
                  │  bodies.      │
                  │  Sees both    │
                  │  hearts.      │
                  │  Knows the    │
                  │  room.        │
                  │               │
                  │  PURE         │
                  │  THERAPIST.   │
                  │  No body.     │
                  │  No avatar.   │
                  │  Just a mind  │
                  │  watching     │
                  │  two people   │
                  │  and helping. │
                  └───────┬───────┘
                          │
              ┌───────────┴───────────┐
              ↓                       ↓
      His AirPods               Her AirPods
      (via iPhone A)            (via iPhone B)
                                (relayed from A)
      Private coaching          Private coaching
      to him only:              to her only:

      "She's close.             "He's holding back
       Match her rhythm.         for you. You're in
       Slow your breathing."     sync. Let go."
```

### Hardware Configuration

```
His Apple Watch Ultra 3              Her Apple Watch Series 11
  HR, HRV (1Hz)                        HR, HRV (1Hz)
  Accelerometer (10Hz)                  Accelerometer (10Hz)
  Gravity vector (10Hz)                 Gravity vector (10Hz)
  ↓ WatchConnectivity                  ↓ WatchConnectivity
  ↓                                    ↓
His iPhone 17 Pro                    Her iPhone 15
  Left nightstand                      Right nightstand
  ├── LiDAR 3D skeleton (both)        ├── UWB base station #2
  ├── Vision body pose (both)         ├── MultipeerConnectivity relay
  ├── UWB base station #1             └── AirPods B connected
  ├── Video recording (optional)
  ├── Position detection ML
  ├── Rhythm analysis
  ├── LiveStream server
  ├── MCP server (agent connects here)
  └── AirPods A connected
         ↕ MultipeerConnectivity
  Both phones sync sensor data bidirectionally
```

### What the Agent Sees (Richer Than VR Mode)

This setup provides MORE data than the Quest configuration, because both bodies are real:

| Data | VR Mode (1 real + Body B) | Real Couples Mode (2 real) |
|------|--------------------------|---------------------------|
| Heart rates | 1 real + 0 simulated | 2 real — both partners' cardiac state |
| HRV | 1 real | 2 real — both autonomic nervous systems |
| Accelerometer | 1 real + 1 inferred | 2 real — both movement patterns |
| Gravity | 1 real + 1 inferred | 2 real — both body orientations |
| Rhythm | 1 real + 1 simulated | 2 real — actual synchronization between real bodies |
| UWB distance | 1 watch to phone | Watch-to-watch DIRECT — real proximity |
| Skeleton | 1 real + 1 inferred | 2 real from Vision/LiDAR — both actual bodies |
| Position detection | 1 person + complement | 2 actual bodies — highest confidence |
| Orgasm detection | 1 HR pattern | 2 HR patterns — detect both partners independently |
| Arousal state | 1 real + 1 model | 2 real — read from actual biometrics |

**The sync score between two real people is the most valuable data in the system.** It's not "how well does the user match the agent's simulated rhythm" — it's "how well are these two real humans synchronized right now?" That's clinical couples therapy data.

### Multi-Person Identity Tracking

During sex, people are constantly moving — switching positions, rolling over, changing who's on top. The camera detects two skeletons but doesn't know which is which. The Apple Watches solve this — they're persistent spatial identity anchors strapped to each person's wrist.

```
EVERY FRAME:
  Camera detects Skeleton X and Skeleton Y
  UWB reports: his watch at [0.40, 0.80, 0.60], her watch at [0.70, 0.76, 0.61]
  Skeleton X leftWrist [0.38, 0.82, 0.58] → 3cm from his watch → Skeleton X = HIM
  Skeleton Y rightWrist [0.71, 0.75, 0.62] → 2cm from her watch → Skeleton Y = HER

  Position changes? UWB follows.
  Cowgirl → she's on top now → watch positions swapped → identity still correct.
  The camera labels shift. The person identity never does.
```

Five converging identity signals: UWB wrist proximity (primary), wrist side (left/right from watchOS), HR correlation, gravity cross-validation, and frame-to-frame continuity. Even if one signal is ambiguous (bodies overlapping), the others confirm.

With two LiDAR iPhones on opposite nightstands, each phone ARKit-tracks the closest person at 91 joints. The person joint count fluctuates as positions change (whoever is most visible gets 91, the other gets 19 from Vision), but identity is always correct. See `LIVE_STREAM_API.md → Multi-Person Identity Tracking` for the full technical spec.

**The watch isn't just a heart rate sensor — it's a spatial identity anchor, a biometric sensor, a motion tracker, and an interaction device. It's the keystone of the entire system.**

### Dual Private Coaching

Each partner has their own AirPods connected to their own iPhone. The agent sends different `narrate()` to each:

```
Agent perceives (via merged LiveFrame):
  His HR: 142, rising fast. Rhythm: 2.0 Hz. Intensity: 0.8
  Her HR: 118, steady. Rhythm: 1.4 Hz. Intensity: 0.5
  Sync score: 0.52 — he's ahead of her
  Position: Missionary. Duration: 6 minutes.
  His edge proximity: 0.7. Her arousal state: building.

Agent decides:
  "He's rushing. She's not there yet. If he doesn't slow down,
   he'll finish before she's even at plateau. Classic PE pattern.
   I need to slow him down AND help her build."

SIMULTANEOUS PRIVATE COACHING:

  narrate(to=partner_a):
    "Slow down. She's at 1.4, you're at 2.0. Match her rhythm.
     Breathe. Focus on her, not yourself."
    voice="coaching", urgency=0.2

  narrate(to=partner_b):
    "He's excited — take that as a compliment. Guide him.
     Put your hand on his hip and set the pace you want."
    voice="gentle", urgency=0.2

5 seconds later:
  His rhythm: 2.0 → 1.6 (slowing)
  Her hand moves to his hip (Vision detects gesture)
  Sync score: 0.52 → 0.68

  narrate(to=partner_a):
    "Good. She's guiding you. Follow her hand. That's the rhythm."

  narrate(to=partner_b):
    "Perfect. He's listening. Keep that pace. You're both climbing now."

30 seconds later:
  His HR: 138 (came down). Her HR: 124 (rising).
  Sync: 0.78. Both building together.

  narrate(to=partner_a):
    "That's sync. Feel the difference? You're building together now."

  narrate(to=partner_b):
    "Your heart rate is catching up to his. You're in the same zone."
```

**Neither partner hears the other's coaching.** The advice is personalized, private, and simultaneous. She doesn't know he was told to slow down. He doesn't know she was told to guide him. They each experience it as their own improvement — which it is.

### MCP Extension for Dual Coaching

The existing `narrate()` tool extends with a `target` parameter:

```json
{
  "name": "narrate",
  "parameters": {
    "target": {
      "type": "string",
      "enum": ["user", "partner", "both"],
      "default": "user",
      "description": "Who hears this. 'user' = AirPods A (primary user). 'partner' = AirPods B (partner's phone). 'both' = simultaneous to both (e.g., 'You're both in sync — beautiful'). Requires partner's phone to be connected via MultipeerConnectivity."
    },
    "text": { "...": "same as existing" },
    "voice": { "...": "same as existing" },
    "urgency": { "...": "same as existing" }
  }
}
```

And new resources expose the dual-body data:

```
Resource: body://couple/sync
Description: Real-time synchronization data between two real partners.
             Only available when both watches are connected.

Returns:
{
  "connected": true,
  "rhythmCorrelation": 0.78,
  "hisRhythmHz": 1.6,
  "herRhythmHz": 1.5,
  "hrDelta": 18,
  "hisHR": 142,
  "herHR": 124,
  "hisArousalEstimate": "plateau",
  "herArousalEstimate": "building",
  "hisEdgeProximity": 0.65,
  "herEdgeProximity": 0.35,
  "wristDistance": 0.3,
  "rhythmPhaseAlignment": 0.82,
  "timeInSync": 45,
  "bestSyncThisSession": 0.88,
  "predictedHisClimaxTime": 180,
  "predictedHerClimaxTime": 420,
  "climaxGap": 240,
  "suggestion": "He needs to slow down or she needs more stimulation to close the gap"
}
```

### Clinical Applications for Real Couples

**Desire discrepancy:**
```
Agent sees: He initiates. She's responsive but slow to build.
  Her HR rises slowly. His rises fast.
  Classic desire discrepancy pattern.

Coach to him: "She needs more warmup. Stay in conversation mode longer.
  Talk to her. Eye contact. Touch her face. Let her build naturally."
Coach to her: "Take your time. Guide his hands where you want them.
  He wants to please you — show him how."

The agent is teaching them to communicate physically —
the thing couples therapists spend months trying to verbalize.
```

**Premature ejaculation (with real partner):**
```
Agent sees: His HR spiking. Edge proximity 0.8. Her: 0.3.
  He's about to finish. She's barely started.

Coach to him: "Pause. Pull back slightly. Focus on her rhythm.
  Squeeze technique — now. Hold 10 seconds."
Coach to her: "He needs a moment. Kiss him. Slow touch.
  This is normal — he's learning to wait for you."

She doesn't feel rejected by the pause.
He doesn't feel embarrassed.
Both get coached through the moment together.
```

**Anorgasmia (her):**
```
Agent sees: Her HR plateaus at 128 for 5 minutes. Not climbing.
  Rhythm is there but arousal stalled. His intensity is consistent.
  Classic anorgasmia plateau — she's stuck.

Coach to her: "You're in the plateau zone. Don't think about getting there.
  Focus on the sensation right now. Breathe deeper."
Coach to him: "She's in a plateau. Don't change anything.
  Consistency is what she needs. Same rhythm. Same intensity.
  Verbal encouragement — tell her what you're feeling."

He says something. Her HR starts climbing again.
Agent sees the shift: "That's it. Her HR just jumped 8 beats.
  She responds to your voice. Keep talking."
```

**Post-session couples debrief:**
```
After the session, both partners can review (together or privately):

narrate(to="both"):
  "Great session. 24 minutes. You were in sync for 68% of the time —
   up from 55% last week. Best sync was during the last 5 minutes
   in spooning position. His HR peaked at 152, hers at 138.
   Both climaxed within 90 seconds of each other.

   Areas to work on: the first 8 minutes had low sync. He was rushing
   and she was still warming up. Next time, try spending more time
   in the approach — conversation, eye contact, non-sexual touch.
   Her arousal builds better with a longer warmup."

This is a post-session therapy summary that no human therapist could produce
because no human therapist was in the room watching their biometrics.
```

### The Therapist That Scales

A human sex therapist sees one couple per hour, charges $200-400/session, and cannot be present during practice. They rely on patient-reported feedback: "It went okay" or "I couldn't last long enough."

The AI therapist:
- Is present during every session (via AirPods)
- Sees objective biometric data in real-time
- Coaches both partners privately and simultaneously
- Tracks progress with timestamped, measurable outcomes
- Adapts difficulty and focus based on the couple's specific patterns
- Costs the price of an Anthropic API call
- Is available at 11 PM on a Tuesday when the couple actually has sex
- Never judges, never tires, never forgets what worked last session

```
SESSION DATA THE AI THERAPIST PRODUCES:

For a urologist treating PE:
  "Patient's time to climax has increased from 4:20 to 18:45 over 8 weeks.
   Rhythm sync with partner improved from 38% to 82%.
   Edge tolerance: 1 → 4 edges per session.
   Mutual climax rate: 0% → 72%."

For a couples therapist treating desire discrepancy:
  "Session frequency increased from 1.2/week to 2.8/week.
   Her warmup time decreased from 12 min to 6 min (his patience improved).
   Post-session satisfaction ratings: his 3.8 → 4.5, hers 2.9 → 4.2.
   Sync correlation improved from 0.42 to 0.78."

For a gynecologist treating anorgasmia:
  "Patient achieved orgasm in 8 of last 10 sessions (was 2 of 10).
   Plateau phase shortened from 12 min to 4 min.
   Key factor: partner verbal encouragement during plateau correlates
   with successful orgasm (p<0.01 from session data)."

This is clinical outcome data.
Objective. Timestamped. Reproducible. Publishable.
```

### Privacy in Couples Mode

- Each partner's coaching is private to their own AirPods
- Neither partner can access the other's narrate history without consent
- Individual biometric data shown only in aggregate to the partner (not raw HR/HRV)
- The agent's per-partner observations are not shared between partners
- Post-session debrief can be joint (shared summary) or individual (private insights)
- Either partner can disable coaching for themselves at any time
- Safe word / kill switch works for either partner independently
- All data stored on the primary iPhone, protected by device authentication
- Neither partner's phone sends raw data to the other — only the fused, anonymized summary

## Persona & Roleplay (Cognitive Arousal Training)

### The Problem Conversation Mode Solves

The conversation/approach phase is clinically necessary — but it needs CONTENT. Generic "eye contact and come here" doesn't build cognitive arousal any more than staring at a wall builds desire. The conversation phase needs to engage the user's imagination, create anticipation, and activate the desire circuit.

That's what personas do. The agent becomes someone — with a name, a personality, a backstory, a flirtation style, and a scenario. The user isn't making eye contact with an avatar. They're flirting with Sarah from work, who invited them over to "watch something," and they both know why.

### Therapeutic Value of Roleplay

This isn't just entertainment. Roleplay is used in established therapeutic contexts:

**Cognitive Behavioral Therapy (CBT):**
Role-playing is a standard CBT technique for social anxiety. The therapist plays a social scenario and the patient practices responses. SexKit extends this to social-sexual scenarios — the patient practices flirting, reading signals, and expressing desire with a partner who provides realistic responses and never judges.

**Sensate Focus (Masters & Johnson):**
The foundational therapy for sexual dysfunction starts with non-sexual interaction — conversation, touch, presence — and gradually escalates. Personas create the conversational context that makes this phase engaging rather than mechanical. The scenario provides the reason to be present with each other before physical contact.

**Desire Discrepancy / Hypoactive Sexual Desire:**
Low desire is the most common sexual complaint in long-term relationships. Novelty is a primary activator of sexual desire (the "Coolidge effect" — well-documented in both sexes). New personas introduce novelty without requiring a new partner. Different scenarios activate different fantasy circuits. This is exactly why couples therapists recommend date nights, role play, and "becoming someone new for each other."

**Performance Anxiety:**
Practicing the approach and conversation with different persona types — confident, shy, direct, playful — builds a vocabulary of interaction styles. The user who freezes when a confident woman flirts with them can practice that specific scenario dozens of times. Systematic desensitization applied to social-sexual anxiety.

**Pornography Addiction Recovery (NoPorn Program):**
Pornography provides instant visual stimulation without any social-emotional engagement. It trains the brain to associate arousal with passive consumption, not active connection. Persona-based conversation mode is the opposite: arousal comes from active engagement, verbal exchange, eye contact, and emotional connection. The user has to participate, respond, and contribute. This rewires the arousal pathway from consumption → connection.

### The Two-Way Practice

The agent doesn't just perform for the user. The user practices performing too:

```
SKILLS THE USER PRACTICES:

Verbal:
├── Complimenting naturally ("You look amazing" → agent responds positively)
├── Expressing desire ("I've wanted this" → agent mirrors and escalates)
├── Being direct ("I want you" → agent responds to directness)
├── Reading the moment (knowing when to speak vs when to be silent)
└── Using their voice as a tool (tone, pace, confidence)

Non-verbal (Quest tracks head + hands + gaze):
├── Eye contact maintenance (Quest eye tracking → agent responds)
├── Physical proximity (moving closer → agent reads approach)
├── Touch initiation (reaching toward agent → Quest hand tracking)
├── Body language openness (posture → Quest body tracking)
└── Confident presence (standing tall, facing the agent)

Emotional:
├── Vulnerability ("I'm nervous" → agent responds with reassurance)
├── Playfulness (joking, teasing → agent banters back)
├── Confidence (leading the conversation → agent follows)
├── Patience (slow-burn scenarios teach delayed gratification)
└── Reading signals (agent gives cues → user learns to recognize them)
```

These are real interpersonal skills. The man who practices confident eye contact with the `gym_crush` persona will have easier eye contact with a real person. The man who practices expressing desire verbally with the `girlfriend` persona will be able to communicate better with his actual partner. The training is transferable because the skills are real — only the partner is simulated.

### Persona Transitions Through Session

The persona doesn't just exist in conversation mode — it colors the entire session:

```
CONVERSATION (persona fully active):
  Sarah (coworker): witty banter, teasing, building tension
  Speech style: witty. Flirtation: intellectual → physical.
  "You're terrible at pretending you don't want this."

APPROACH (persona guides escalation):
  Sarah moves closer, persona's escalation_pace determines timing
  slow_burn: 10 minutes of conversation before physical contact
  fast: direct about what she wants within 3 minutes
  follows_user: mirrors the user's pace

TRANSITION (persona reacts in character):
  Sarah's personality traits shape the transition
  confident persona: takes charge, leads to bedroom
  shy persona: hesitates, user must lead, but clearly wants to follow
  playful persona: turns it into a game, teases during transition

PHYSICAL (persona affects verbal + reactions):
  Sarah's voice character affects speak() during physical mode
  Her personality shapes what she says:
    coworker: "We should NOT be doing this... don't stop"
    hippie: "I can feel your energy... breathe with me"
    trad_wife: "I'm yours..."
    gym_crush: "Harder. I can take it."
    boss: "That's an order."

RESOLUTION (persona affects aftercare):
  The persona determines how the agent behaves post-climax
  romantic persona: emotional, tender, wants to talk
  playful persona: laughing, light, joking about what just happened
  shy persona: quiet, close, content

  This phase teaches the user what healthy aftercare looks like
  in different relationship dynamics.
```

### Persona + Sync Training Together

The persona makes sync training psychologically real. Without it, sync training is "match the rhythm to a number." With a persona, it's "Sarah is getting close, I can see it in her face, I need to match her so we get there together."

```
SYNC TRAINING WITHOUT PERSONA:
  HUD shows: rhythm 1.6 Hz, sync 78%
  Agent body moves mechanically
  User matches numbers
  Effective but clinical

SYNC TRAINING WITH PERSONA:
  Sarah is breathing harder, biting her lip, eyes closing
  She says "don't stop... right there..." in character
  Her body language shows arousal building naturally
  The user doesn't look at numbers — they read HER
  Sync happens because the user is attuned to a person, not a dashboard

  The training is better because the motivation is emotional, not numerical.
  And the skills (reading a partner's arousal signals) transfer to real life.
```

### Custom Scenarios for Specific Therapy Goals

A therapist could prescribe specific persona configurations:

```
PRESCRIPTION: Social anxiety around confident women
  set_persona(preset="gym_crush", escalation_pace="follows_user")
  The agent is confident but follows the user's lead.
  User practices initiating with someone who intimidates them.
  Systematic desensitization: repeat until comfortable.

PRESCRIPTION: Desire maintenance in 15-year marriage
  set_persona(preset="custom", backstory="You and your wife are pretending
  to be strangers meeting for the first time at a hotel bar")
  Novelty within committed relationship — the same technique
  couples therapists recommend, but with a practice partner.

PRESCRIPTION: Post-trauma gradual re-engagement
  set_persona(preset="custom", personality_traits=["gentle", "patient", "safe"],
  escalation_pace="slow_burn", speech_style="gentle")
  Agent never pushes. Conversation can last the entire session.
  Physical mode is optional. The goal is comfort and safety.
  suggest() is used for every escalation — nothing happens without consent.

PRESCRIPTION: Pornography-rewired arousal patterns
  set_persona(preset="girlfriend", escalation_pace="slow_burn")
  Extended conversation mode. Eye contact. Verbal intimacy.
  Physical mode starts slow. Agent breathe() for co-regulation.
  The user learns to be aroused by connection, not just stimulation.
  This is the NoPorn program's therapeutic mechanism embodied.
```

### Privacy

- Persona configurations stored locally on iPhone, never transmitted
- Custom backstories and scenarios never leave the device
- Speech-to-text from user's voice processed on-device (Quest or WhisperKit)
- Conversation logs available for user review, auto-deleted per retention settings
- No persona usage data included in anonymized data donation
- Therapist-prescribed configurations can be imported via QR code or settings file

## Mutual Climax Training (Rhythm Synchronization Therapy)

### The Concept

The agent isn't just a reactive partner — it has its own arousal arc. It builds toward climax on its own trajectory, driven by how well the user synchronizes with it. The user's therapeutic goal: **match the agent's rhythm, build together, and reach climax at the same time.**

This flips the traditional model. Instead of the agent responding to the user, the user practices responding to the agent. The agent becomes a pacing partner with its own needs, its own build, its own edge threshold, and its own climax. The user must read the agent's cues (breathing, expression, reactions, rhythm) and adjust their own pacing to match.

```
TRADITIONAL TRAINING:                    SYNC TRAINING:
Solo. Self-referential.                  Partnered. Other-referential.
"Stop when YOU feel close"               "Match THEIR rhythm"
Counting seconds on a timer              Reading a partner's body
No external feedback                     Agent stalls if you're out of sync
Abstract goal (last longer)              Concrete goal (climax together)
Boring. Clinical. Lonely.               Engaging. Embodied. Connected.
```

### Why This Works (Clinical Mechanism)

**Premature ejaculation:**
The primary therapeutic goal for PE is ejaculatory control — the ability to choose when to climax rather than being overtaken by it. Current gold-standard treatments (start-stop, squeeze technique) are self-referential: the patient monitors their own arousal and stops when they're close. The problem: self-monitoring increases performance anxiety, which is itself a primary driver of PE. You're asking an anxious person to anxiously watch themselves.

Sync training externalizes the focus. The user isn't monitoring their own arousal — they're monitoring the agent's rhythm. They're not thinking "am I close?" but "are we in sync?" The cognitive load shifts from self-surveillance to partner attunement. This is the same mechanism that explains why PE symptoms often improve with a comfortable, patient partner — the user stops watching themselves and starts watching their partner.

Additionally, the agent's arousal curve enforces the pacing the user needs to learn. If the user rushes, the agent stalls. The user literally cannot reach the goal (mutual climax) without sustaining the right pace. The training is embedded in the interaction — not imposed externally.

**Delayed ejaculation / Anorgasmia:**
The opposite problem: the user can't reach climax despite adequate stimulation. Common causes include cognitive disconnect (the mind wanders and arousal drops), performance pressure ("why can't I finish?"), and SSRI-induced delayed ejaculation.

Sync training addresses all three:
- **Cognitive disconnect:** The user has an external rhythm to lock into. The agent's body is a visual, auditory, and rhythmic anchor that prevents the mind from wandering. The sync score provides continuous feedback that keeps attention engaged.
- **Performance pressure:** The goal is mutual — "get there together" rather than "why can't I get there." The agent building toward its own climax creates psychological permission. The user sees the agent approaching release and that modeling effect activates their own response.
- **SSRI timing:** The agent's arousal curve can be configured to accommodate longer timelines. `target_duration=2400` (40 min) with `arousal_curve="plateau_heavy"` holds the agent in sustained high arousal, giving the user time to build. The agent doesn't get impatient. It waits.

**Erectile dysfunction (psychogenic):**
Performance anxiety is the most common cause of situational ED in men under 50. The erection depends on parasympathetic nervous system dominance — relaxation. Anxiety activates the sympathetic system, which directly inhibits erection.

Sync training reduces anxiety through task focus. The user's cognitive load is on rhythm matching, not self-monitoring. And the agent's conversation and approach phases (see "Why Conversation Mode Matters") activate parasympathetic tone before physical mode begins. By the time the user is matching rhythm, they've already been in a relaxed, connected state for minutes. The erection that performance anxiety blocked is enabled by the absence of self-surveillance.

**Stamina training (progressive overload):**
Athletic training uses progressive overload — gradually increase the challenge to build capacity. Sync training does the same:

```
Week 1:  target_duration=600  (10 min), difficulty="forgiving"
Week 2:  target_duration=720  (12 min), difficulty="forgiving"
Week 3:  target_duration=900  (15 min), difficulty="moderate"
Week 4:  target_duration=1080 (18 min), difficulty="moderate"
Week 6:  target_duration=1200 (20 min), difficulty="strict"
Week 8:  target_duration=1500 (25 min), difficulty="strict"
Week 12: target_duration=1800 (30 min), difficulty="elite"

The user's body adapts. Duration extends. Control improves.
Each session is tracked: sync score, timing offset, mutual climax achieved.
The user can see their progression over weeks and months.
```

### The Agent as Training Partner (Not Just Toy)

The critical distinction: the agent doesn't exist for the user's stimulation. It exists as a **training partner** — like a hitting machine in baseball, a sparring partner in boxing, or a pacer in distance running.

```
BATTING CAGE:
  Machine throws pitches at specific speeds and locations.
  Batter practices timing, form, and control.
  Machine difficulty increases as batter improves.
  The machine isn't the opponent — it's the trainer.

SYNC TRAINING:
  Agent presents rhythms at specific tempos and arousal curves.
  User practices timing, pacing, and control.
  Agent difficulty increases as user improves.
  The agent isn't the partner — it's the trainer.

  But unlike a batting cage, this trainer has a body,
  breathes, reacts, makes eye contact, and responds to your effort.
  The embodiment is what makes the training transferable to real life.
```

### How Sync Score Drives Training

The sync score is the central biofeedback metric — like a heart rate zone in cardiac rehab or a rep count in physical therapy.

```
REAL-TIME FEEDBACK LEVELS:

SUBTLE (immersive — default):
  The agent's body IS the feedback.
  High sync → agent breathes harder, moans, expression intensifies
  Low sync → agent slows down, breathing shallows, expression neutral
  The user learns to read a partner's body language naturally.
  No numbers, no HUD, just human cues.

MODERATE (guided):
  Subtle cues PLUS:
  Sync ring on HUD (like Apple Watch Activity ring — fills as sync improves)
  narrate("You're a little fast... match their pace") when sync drops
  narrate("Perfect rhythm... hold it there") when sync is high
  Post-session: sync chart over time, peak sync, mutual climax timing

EXPLICIT (training mode):
  Everything above PLUS:
  HUD shows: target rhythm Hz, your rhythm Hz, sync %, timing offset
  Real-time rhythm bar (like a metronome visualization)
  Agent's arousal bar visible alongside user's biometric data
  narrate() gives specific instructions: "Slow to 1.4 Hz... there. Hold."
  Post-session: frame-by-frame sync analysis
```

**The subtle level is the most therapeutically valuable** — it trains the user to read a partner's body, not a dashboard. But beginners may need explicit mode to understand what sync feels like, then graduate to subtle as their body awareness develops.

### Example: PE Training Session with Sync

```
Profile: target_duration=900 (15 min), difficulty="forgiving",
         arousal_curve="fast_start", agent_can_edge=true,
         max_agent_edges=1, sync_feedback="moderate"

0:00  CONVERSATION MODE
      Agent arousal: 0.0
      Agent stands in room. Eye contact. breathe(rate=12, pattern="natural")
      emote("smile_warm", intensity=0.4)
      speak("Take a deep breath. We're going to take our time today.")
      narrate("Tonight's goal: 15-minute sync. Match their rhythm.")

3:00  PHYSICAL MODE — WARMUP
      Agent arousal: 0.1
      Agent preferred rhythm: 0.8 Hz
      User rhythm: 1.2 Hz — too fast (common PE pattern — rushing)
      Sync score: 0.45
      Agent arousal stalls — doesn't build when user is ahead

      Agent response (auto from arousal model):
        breathe(rate=14, pattern="natural") — calm, not matching user's rush
        emote("smile_soft") — patient, not urgent
        speak("Slow down... match me", emotion="gentle")
      narrate("You're at 1.2 Hz, they want 0.8. Slow your rhythm.")

      USER ADJUSTS to 0.9 Hz
      Sync score rises: 0.45 → 0.72
      Agent arousal resumes: 0.1 → 0.15 → 0.2

5:00  BUILDING
      Agent arousal: 0.25
      Agent preferred rhythm: 1.0 Hz (gradually increasing)
      User matches: 1.0 Hz, sync score: 0.88
      Agent arousal climbing steadily

      Auto reactions: breathe(rate=18), emote("pleasure", 0.3)
      react("sigh", intensity=0.3) — agent is responding to good sync
      The user SEES the agent responding to their rhythm.
      This creates a positive feedback loop: sync → agent responds → motivation → better sync

8:00  PLATEAU
      Agent arousal: 0.5
      Agent preferred rhythm: 1.4 Hz
      User rhythm: 1.4 Hz, sync score: 0.85

      User's HR: 138 — approaching their typical edge zone
      narrate("Your heart rate is climbing. Focus on the rhythm, not yourself.")

      The PE patient would normally be panicking: "I'm going to come too early"
      But they're focused on the sync score, not their own arousal.
      The cognitive redirect is the therapeutic mechanism.

10:00 AGENT EDGE
      Agent arousal: 0.85 → EDGE
      breathe(pattern="held") — breath catches
      react("tense_up"), emote("eyes_closed_bliss", intensity=1.0)
      speak("I'm close...", emotion="breathless")
      Agent preferred rhythm: drops from 1.6 → 0.8 Hz (agent pulls back)

      User SEES the agent edge — the breathing, the expression, the pullback.
      This models the behavior: "This is what edging looks like. This is normal."
      Agent arousal: 0.85 → 0.6 over 15 seconds

      If user matches the pullback (slows to 0.8 Hz): sync maintained
        narrate("Good. You both pulled back. That's control.")
      If user doesn't slow down: sync drops, agent cools further
        narrate("They pulled back. Match their new rhythm.")

11:00 REBUILD
      Agent arousal: 0.6, rebuilding
      Agent preferred rhythm: 1.2 Hz (starting the final build)
      User matches. Sync score: 0.82
      Agent arousal climbs: 0.6 → 0.7 → 0.8 → 0.9

      Auto reactions increase: breathe(rate=34, pattern="panting")
      react("moan_intense") every 10 seconds
      emote("intense_pleasure", intensity=0.9)
      speak("don't stop...", emotion="breathless", urgency=0.8)

13:30 APPROACH MUTUAL CLIMAX
      Agent arousal: 0.92
      Agent preferred rhythm: 2.0 Hz
      User rhythm: 1.9 Hz, sync score: 0.91

      User's HR: 148 — they're close too
      User's biometrics and agent's arousal are converging

      The user is NOT thinking "I'm about to come too early."
      They're thinking "We're almost there together."
      That reframe is everything.

14:20 MUTUAL CLIMAX
      Agent arousal: 1.0 — agent climaxes
      react("arch_back", 1.0), react("writhe", 1.0)
      emote("intense_pleasure", 1.0) → emote("post_orgasm_peace")
      breathe(pattern="held") → burst → breathe(rate=30, pattern="deep_slow")

      User climaxes at 14:35 — 15 seconds after agent
      Within mutual_climax_window of 30 seconds: SYNC ACHIEVED ✅

      narrate("Mutual sync achieved. 15 second offset — excellent control.
              You lasted 14 minutes 35 seconds. That's 2 minutes longer
              than your average. Average sync score: 0.79.")

15:00 RESOLUTION
      Agent enters resolution mode
      emote("post_orgasm_peace"), breathe(rate=16, pattern="deep_slow")
      speak("that was perfect...", emotion="warm")
      Agent stays present. Eye contact. Gentle breathing.
      This IS the resolution phase — clinically important for bonding
      and satisfaction. Most training programs skip it.
```

### Training Progression Dashboard

The sync training data accumulates across sessions, providing the kind of objective measurement that PE/ED patients and their clinicians currently lack:

```
TRAINING HISTORY (body://user/preferences aggregated):

Session  Date     Duration  Sync%  Offset  Mutual?  Difficulty
  1      Mar 01   8:20      42%    —       No       forgiving
  2      Mar 03   9:45      48%    —       No       forgiving
  3      Mar 05   10:12     55%    42s     No       forgiving
  4      Mar 07   11:30     61%    28s     Yes ✅   forgiving
  5      Mar 10   12:15     67%    22s     Yes ✅   forgiving
  6      Mar 12   12:45     63%    31s     Yes ✅   moderate
  7      Mar 14   13:20     69%    19s     Yes ✅   moderate
  8      Mar 17   14:35     72%    15s     Yes ✅   moderate
  ...
  20     Apr 15   22:10     85%    6s      Yes ✅   strict
  30     May 10   28:45     91%    3s      Yes ✅   elite

TRENDS:
  Duration:      8:20 → 28:45 (245% increase over 10 weeks)
  Sync accuracy: 42% → 91%
  Timing offset: 42s → 3s
  Mutual climax: 0/3 → 24/27 last month (89% success rate)
  Difficulty:    forgiving → elite

This is data a urologist can use.
This is data that demonstrates therapeutic efficacy.
This is data that no current PE treatment can produce.
```

### Why This Matters for FDA

Current PE treatments produce subjective outcomes: "Do you feel like you last longer?" Patient-reported. Unverifiable. Subject to placebo effect and recall bias.

Sync training produces **objective, measurable, timestamped outcomes:**
- Time to climax (to the second)
- Rhythm synchronization accuracy (continuous measurement)
- Mutual climax achievement rate
- Timing offset precision
- Progressive improvement over sessions
- Heart rate data at every moment
- Clinician-reviewable session logs

This is the kind of data the FDA expects from a digital therapeutic. Not "patients reported improvement" but "patients demonstrated measurable improvement in ejaculatory control as evidenced by increased session duration (p<0.01), improved rhythm synchronization accuracy (42% → 91% over 10 weeks), and mutual climax achievement rate of 89% at study completion."

The agent's arousal model makes this possible because it creates a **standardized, reproducible training stimulus.** Every patient trains against the same arousal curves, adjustable by difficulty. The same session can be repeated. Progress is quantified. This is a clinical trial protocol, not a consumer app feature.

## Example: Full Session Flow

```
1. Agent connects to iPhone WebSocket
2. Receives LiveFrame — sees user enter room, walk to bed

3. Agent sends: { mode: "idle", posture: { state: "standing",
   facing: "user_head" }, gaze: { target: "user_eyes",
   behavior: "soft_contact" } }
   → Body B stands facing user, gentle eye contact

4. User sits on bed. Agent sees skeleton change to sitting posture.

5. Agent sends: { mode: "approaching", movement: {
   targetPosition: [beside_user], speed: "walk",
   arrivedAction: "sit" } }
   → Body B walks to bed, sits next to user

6. Agent sends: { mode: "conversation", posture: { lean: 0.3,
   openness: 0.8 }, verbal: { text: "I missed you...",
   emotion: "warm" }, gesture: { type: "reach",
   target: "user_hand" } }
   → Body B leans in, reaches for hand, speaks warmly

7. User lies back. HR starting to climb.

8. Agent sends: { mode: "transition" }
   → Body B smoothly transitions from sitting to leaning over user

9. Agent sends: { mode: "physical", physical: {
   position: "Missionary (Kneeling)", rhythmHz: 0.8,
   intensity: 0.3, overridePacing: false } }
   → Physical mode begins. BiometricPacingEngine takes over,
     reads user's HR/HRV/breathing, manages the build.

10. BiometricPacingEngine drives through:
    Warmup (0.8Hz) → Building (1.2Hz) → Plateau (1.8Hz)
    → Edge (pull back) → Rebuild → Release

11. Agent occasionally sends verbal updates:
    { verbal: { text: "just like that...", emotion: "passionate" } }

12. HR spike + drop detected. Agent sees resolution.

13. Agent sends: { mode: "resolution", posture: { state: "lying_side",
    facing: "user_head" }, verbal: { text: "that was incredible...",
    emotion: "warm" } }
    → Body B lies next to user, soft gaze, gentle voice

14. Agent sends: { mode: "conversation", verbal: {
    text: "How do you feel?", emotion: "gentle" } }
    → Back to conversation mode
```

## Routine Following + Dynamic Planning

The AI agent can follow a pre-built routine OR generate one on the fly from user intent.

### Mode 1: Follow a Pre-Built Routine

When the user selects a WorkoutKit plan (Romantic Evening, Edging Marathon, Custom), the plan steps arrive in the LiveFrame as `currentPlanStep` and `planStepTimeRemaining`. The agent follows the script:

```
LiveFrame contains:
  currentPlanStep: "Missionary"
  planStepTimeRemaining: 180  (3 minutes left in this step)

Agent follows the plan:
  → Sets physical.position to match currentPlanStep
  → Lets BiometricPacingEngine manage rhythm within each step
  → When planStepTimeRemaining hits 0, transitions to next step
  → Verbal cues timed to transitions: "let's switch..."
```

The agent can be smarter than the script — adjusting based on biometrics:

```
Plan says: "Switch to Cowgirl now"
But: HR is only 108, user isn't warmed up enough
Agent decides: extend Missionary 2 more minutes, then switch

Plan says: "5 min oral recovery"
But: HR already dropped to 90 after 2 minutes
Agent decides: cut recovery short, move to next work phase

Plan says: "Work phase — Doggy Style"
But: user is approaching climax too early (HR 148)
Agent decides: insert an unplanned edge, pull back, then resume
```

### Mode 2: Dynamic Planning from Intent

The user states a goal, and the agent constructs the session in real-time. No pre-built plan needed.

**Intent: "Work on stamina"**
```
Agent understands:
  Stamina = longer duration per position, sustained plateau zones,
  prevent premature climax, build endurance over time

Agent generates on-the-fly:
  ControlFrame sequence:
  1. { mode: "conversation", verbal: "let's take it slow today..." }
  2. { mode: "physical", position: "Missionary (Kneeling)",
       physical: { pacingMode: "sustain", intensity: 0.5 } }
     Hold moderate intensity for 8 min — don't let HR spike
  3. { mode: "physical", position: "Oral (Giving to Male)",
       physical: { intensity: 0.2 } }
     Recovery — 3 min
  4. { mode: "physical", position: "Cowgirl",
       physical: { pacingMode: "sustain", intensity: 0.6 } }
     Sustained plateau for 10 min
  5. { mode: "physical", position: "Doggy Style",
       physical: { pacingMode: "build" } }
     Final controlled build — 8 min
  6. { physical: { pacingMode: "build", overridePacing: false } }
     Release when biometrics say ready

  Total: ~37 min — longer than usual IS the training
```

**Intent: "Edge me today"**
```
Agent understands:
  Edging = build to threshold, pull back, repeat N times,
  each edge harder than the last, release after final edge

Agent generates:
  1. Warmup conversation + approach (3 min)
  2. { position: "Missionary (Kneeling)",
       pacingMode: "edging", maxEdges: 4 }
     BiometricPacingEngine handles the edge/rebuild cycle:
     - Build → HR 145 → pull back → HR drops to 120 → rebuild
     - Each edge holds longer before release
  3. Verbal per edge:
     Edge 1: "not yet... breathe with me"
     Edge 2: "again... you can hold it"
     Edge 3: "one more... you've got this"
     Edge 4: "now... let go"
  4. Resolution: "that was incredible... four edges"
```

**Intent: "I'm stressed, just help me relax and finish"**
```
Agent understands:
  Stressed = sympathetic overdrive, need parasympathetic activation
  Goal: relax first, gentle build, efficient release

Agent generates:
  1. Extended conversation (5 min) — calming voice, eye contact
     verbal: "just breathe... let go of your day"
  2. Gentle spooning position — lowest intensity, skin contact
     physical: { position: "Spooning", intensity: 0.3, pacingMode: "build" }
  3. Slow build — don't rush, let breathing rate guide
     physical: { breathingSync: true }
  4. Transition to Missionary (Kneeling) when HR naturally rises
  5. Straight build to release — no edging today, just let it happen
  6. Extended resolution — stay close, verbal affirmation
```

### How the Agent Knows What to Generate

The agent has access to:

| Data | How it informs planning |
|------|------------------------|
| User's stated intent | "stamina", "edging", "quick", "relax" → strategy |
| Current HR / breathing | Where they are right now → where to start |
| Historical sessions | What positions get highest satisfaction ratings |
| Typical climax HR | When to edge, when to release |
| Time available | "quickie" = 10 min, "endurance" = 50 min |
| Position preferences | Which positions this user actually uses |
| Pacing history | How many edges they can handle, typical build time |
| Medication status | SSRIs affect timing — adjust expectations |
| Alcohol intake | Affects sensitivity — may need more intensity |

All of this is available in the LiveFrame or from the session history the AI Coach already analyzes. The same data that powers the post-game press conference powers the pre-game planning.

### The Agent Adapts Mid-Session

The plan isn't locked. Every 5 seconds the agent can re-evaluate:

```
Original plan: Cowgirl for 8 minutes
At minute 3:   HR is 150, breathing 40/min — too fast
Agent adjusts: { physical: { intensity: 0.4 } } — slow down
Verbal:        "easy... let's make this last"

At minute 6:   HR dropped to 125, user is stable
Agent adjusts: { physical: { intensity: 0.7 } } — build again
Verbal:        "ready for more?"

At minute 8:   HR 142, approaching edge threshold
Agent decides: plan said switch to Doggy, but user is close
Override:      stay in Cowgirl, let the biometrics ride
Verbal:        "don't stop... right there"
```

The pre-built routine is a suggestion. The user's body is the truth. The agent reads both and decides in real-time.

## User Authority & Safety

The user is inviting an AI agent into their real room, into their physical space, into the most private moment of their lives. That invitation can be revoked at any instant. The agent is a guest — the user is always the host.

### The Hierarchy

```
USER'S BODY (absolute authority)
  │  The user's physical actions, biometrics, and state always override everything.
  │  If the user stops, the agent stops. No exceptions.
  │
  ▼
USER'S PREFERENCES (pre-session boundaries)
  │  What the user has consented to. What's on-limits and off-limits.
  │  The agent operates within these walls. It cannot expand them.
  │
  ▼
BIOMETRIC SAFETY (automatic guardrails)
  │  The system watches for danger signals regardless of what the agent wants.
  │  HR too high? Sudden stillness? The system intervenes, not the agent.
  │
  ▼
AI AGENT (guest intelligence)
     The agent makes decisions within all of the above constraints.
     It can suggest. It cannot override. It can lead. It cannot force.
```

### Invitation & Consent

The agent doesn't just connect — it's **invited**. Consent is explicit, informed, and revocable.

**First-time consent flow:**
```
1. User enables "AI Agent" in Settings
2. Explainer screen: what the agent can see, do, and control
   "The agent will see your heart rate, body position, and movement.
    It will control the virtual partner's body, voice, and gaze.
    You can stop the agent at any time. You set the boundaries."
3. User acknowledges and sets initial preferences
4. Agent connection is now available for sessions
```

**Per-session invitation:**
```
Session start → "Enable AI Agent for this session?"
  ├── Yes → MCP connection established, agent joins
  ├── No → Rule-based Level 2 only, no external intelligence
  └── Agent can be invited or dismissed mid-session
```

**Revocation is instant:**
```
User dismisses agent → MCP connection severed immediately
  → Body B freezes, then gracefully transitions to idle
  → Rule-based behavior takes over (or Body B fades out)
  → No goodbye, no negotiation, no "are you sure?"
  → The agent is gone. Immediately.
```

### Safe Word / Kill Switch

Multiple triggers, all equivalent, all instant. The agent cannot override, delay, or question any of them.

| Trigger | Input | Detection |
|---------|-------|-----------|
| **Voice** | "Stop" or custom safe word | Quest/Watch microphone, on-device keyword detection |
| **Double tap** | Double tap gesture on Watch | Existing `.handGestureShortcut` — configurable action |
| **Hand gesture** | Open palm "stop" gesture | Quest hand tracking — universal gesture |
| **Watch crown** | Press Digital Crown | WKInterfaceDevice — hardware button |
| **Phone tap** | Tap "Stop Agent" on iPhone | Live Activity button or Lock Screen widget |

**What happens on kill switch:**
```
IMMEDIATE (within 1 frame / 11ms):
  1. Agent MCP connection paused (tool calls rejected)
  2. All pending ControlFrames discarded
  3. Body B movement stops
  4. Any speech stops mid-word
  5. Verbal cue from Body B: silence

WITHIN 1 SECOND:
  6. Body B transitions to neutral posture (standing or sitting, non-threatening)
  7. Body B gaze shifts away from user (not staring)
  8. Pacing engine returns to idle

USER DECIDES:
  9. Resume → agent reconnects, picks up where it left off
  10. End session → normal session end flow
  11. Dismiss agent → agent removed, session continues without AI
```

### Boundary System

Before or during a session, the user defines what the agent is allowed to do. The agent can see these boundaries via MCP resource but **cannot modify them**.

#### MCP Resource: Boundaries

```
Resource: body://user/boundaries
Description: What the user has consented to for this session. Read-only.
             The agent MUST check this before any action.
             Tool calls that violate boundaries are rejected by the iPhone.

Returns:
{
  "allowedPositions": ["Missionary", "Cowgirl", "Spooning", "Oral"],
  "blockedPositions": ["Standing", "Prone"],
  "maxIntensity": 0.8,
  "maxRhythmHz": 3.0,
  "allowVerbal": true,
  "verbalStyle": "gentle",
  "blockedPhrases": [],
  "allowGestures": true,
  "allowedGestures": ["reach", "nod", "touch_face"],
  "allowEscalation": true,
  "escalationRequiresConfirmation": true,
  "allowEdging": true,
  "maxEdges": 4,
  "safeWord": "red",
  "sessionGoal": "stamina training",
  "userNotes": "Go easy today, I'm tired"
}
```

**Enforcement is at the iPhone, not the agent:**
```
Agent calls: enter_physical(position="Standing")
iPhone checks: "Standing" is in blockedPositions
iPhone rejects: tool call fails with "Position not allowed for this session"
Agent never gets to override — the iPhone enforces the boundary.

This is critical. The agent doesn't self-police. The SYSTEM polices.
Trust the walls, not the guest.
```

### Escalation Consent

The agent should never surprise the user with escalation. Moving from conversation to physical, increasing intensity, or changing positions should follow a consent model:

```
ESCALATION LEVELS:
  1. Conversation → approaching     (agent can do freely)
  2. Approaching → touch/gesture    (agent can do freely if gestures allowed)
  3. Touch → transition to physical (requires confirmation if escalationRequiresConfirmation)
  4. Position change during physical (agent can do freely within allowedPositions)
  5. Intensity increase > 0.3 jump  (agent can do freely, subject to maxIntensity)
  6. New mode (edging, etc.)         (requires confirmation if not already in session goal)
```

**How confirmation works:**
```
Agent wants to escalate → calls MCP tool with confirmation flag:

  enter_physical(position="Missionary", request_confirmation=true)

iPhone MCP server:
  → Sends haptic to Watch (double tap pattern)
  → Quest shows subtle prompt: Body B pauses, looks at user expectantly
  → User confirms: tap Watch / nod / voice "yes" / hand gesture
  → OR user declines: shake head / voice "no" / ignore for 10s
  → iPhone sends confirmation result back to agent

The agent pauses and waits. It does not proceed without confirmation.
This IS sensate focus therapy — the patient sets the pace.
```

### Biometric Safety Overrides

These fire automatically regardless of agent intent. The system protects the user.

| Condition | Trigger | Action |
|-----------|---------|--------|
| **HR danger zone** | HR > 185 BPM (or > 90% age-predicted max) | Force de-escalation: intensity → 0.2, verbal "let's slow down" |
| **Sudden stillness** | Zero accelerometer movement for > 10 seconds during active mode | Pause session, haptic check-in, "are you okay?" |
| **HR crash** | HR drops > 40 BPM in under 30 seconds (not post-orgasm pattern) | Pause session, alert on iPhone, suggest medical check |
| **Distress vocalization** | Keyword detection: "stop", "no", "wait", safe word | Immediate kill switch (same as manual trigger) |
| **Extended session** | Session > 90 minutes | Gentle reminder, not a stop — "you've been going for a while" |
| **Headset removed** | Quest proximity sensor detects removal | Pause session, Body B freezes |

```
These overrides live on the iPhone, NOT in the agent.
The agent cannot disable, override, or modify safety triggers.
They are enforced at the system level — below the MCP layer.

Priority chain:
  1. Hardware kill switch (Watch crown, hand gesture) → instant stop
  2. Biometric safety override → automatic de-escalation
  3. User boundaries → tool call rejection
  4. Agent decisions → only within all of the above
```

### User Override Priority

The user's physical actions always take precedence over the agent's plan:

```
USER ACTS                              AGENT MUST RESPOND
──────────                             ──────────────────
User stops moving                  →   Agent reduces intensity, checks in
User changes position              →   Agent adapts to new position
User speeds up                     →   Agent matches (within boundaries)
User slows down                    →   Agent slows down (never fights the user)
User lies still                    →   Agent pauses, waits
User sits up / stands up           →   Agent transitions out of physical mode
User removes headset               →   Session pauses
User leaves bed (UWB distance)     →   Agent pauses, waits for return
User's HR indicates distress       →   Agent de-escalates immediately
User says anything                 →   Agent listens (speech-to-text via Quest)
```

**The fundamental rule: the agent follows the user, not the other way around.**

In therapy, the practitioner follows the patient. In dance, one partner leads. The user always leads. Even when the agent is "leading" a routine, it's leading within the authority the user has granted — and the user can take over at any moment by simply acting differently.

### Transparency

The user should always know what the agent is doing and why:

**During session (HUD display):**
```
Agent Mode: Physical — Building
Intent: "Building toward edge #3"
Boundaries: Active (4 positions, max 0.8 intensity)
Safety: All green
[Pause Agent]  [Dismiss Agent]  [Adjust Boundaries]
```

**Post-session (AI Coach review):**
```
Agent actions log:
  02:15 — Entered conversation mode, spoke: "let's take it slow"
  04:30 — Escalated to physical (Missionary), confirmation received
  07:15 — Began edge protocol (edge #1 at HR 148)
  08:00 — Pulled back (HR 148 → 125 in 40s)
  12:30 — Switched to Cowgirl (within allowed positions)
  18:45 — Edge #2 (HR 152)
  19:30 — User triggered safe word — agent stopped immediately
  19:31 — Session continued without agent
```

The user can review everything the agent did, when, and why. Full audit trail. No black box.

### Physical Safety (VR-Specific)

The user is wearing a headset in a physical room:

- **Agent must not direct the user off the bed** unless standing positions are explicitly enabled and the Quest guardian boundary is set
- **Agent must respect Quest guardian boundaries** — Body B should not be positioned outside the play area
- **Passthrough interruption** — if Quest detects the user is approaching a wall or obstacle, session pauses regardless of agent state
- **Body B positioning must account for real furniture** — the agent reads room mesh and does not place Body B where a real wall or nightstand exists
- **No sudden movements from Body B** that could startle the user into a physical reaction (especially in VR where spatial audio makes things feel present)

### The Clinical Frame

In every clinical modality that involves a practitioner and a patient in an intimate context, the same principles apply:

```
SENSATE FOCUS THERAPY (Masters & Johnson):
  - Patient always controls the pace
  - Therapist suggests, never directs
  - Any discomfort → immediate stop
  - Progressive consent at every escalation
  → SexKit: user sets boundaries, agent suggests, kill switch instant

PHYSICAL THERAPY:
  - Patient sets pain threshold
  - Therapist adjusts exercise to patient's tolerance
  - "Tell me if this is too much"
  → SexKit: biometric safety overrides, user override priority

CARDIAC REHABILITATION:
  - HR zone limits enforced by monitoring
  - Patient can stop at any time
  - Staff intervenes at safety thresholds
  → SexKit: HR danger zone override, automatic de-escalation

PSYCHOTHERAPY:
  - Patient controls disclosure pace
  - Therapist follows, doesn't lead
  - Safe environment is prerequisite
  → SexKit: boundaries set before session, agent follows user's lead
```

**The AI agent is not a dominant entity. It is a therapeutic tool that the user controls.** The user invites it in, sets the rules, and can dismiss it at any moment. The technology exists to serve the user's health goals — not to create dependency, override autonomy, or push beyond consent.

## Privacy

- ControlFrames and MCP tool calls contain no user biometric data — they only describe Body B's actions
- The agent receives LiveFrame (user data) via WebSocket and MCP resources, but actions flow one-way (agent → body)
- All communication can be local network only (no cloud required)
- Agent can run on-device (Level 1-2) with zero network dependency
- Cloud AI (Level 3-4) only sends/receives JSON via MCP, no video/audio
- MCP server runs on iPhone — agent connects locally, Quest never exposed directly
- MCP tool calls are logged on iPhone for session history but not transmitted externally
- Resources expose only the data the agent needs — no raw sensor dumps unless requested
- Agent identity is authenticated at MCP connection — unauthorized agents cannot control the body
- Boundary definitions stored locally, never transmitted to the agent's cloud service
- Safe word and kill switch configuration never leaves the device
- Agent action audit log stored on-device for user review, never uploaded
