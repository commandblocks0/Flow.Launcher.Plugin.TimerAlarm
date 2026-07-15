# Timer & Alarm

A Flow Launcher plugin for quickly creating timers and alarms.

> **Important:** Timers and alarms do not persist after Flow Launcher is closed or restarted.

## Features

- Create timers and alarms
- Desktop overlay showing active timers and alarms
- Overlay is visible only while the Flow Launcher window is open

## Usage

### Timers

```text
timer 5 30      - Creates a 5m 30s timer
timer 330       - Creates a 5m 30s timer
timer food 5 0  - Creates a 5m timer named "food"
```

### Alarms

```text
alarm 12 30      - Creates an alarm for 12:30
alarm food 12 0  - Creates an alarm named "food" for 12:00
```

### Delete

```text
timer del food
alarm del 1
```
