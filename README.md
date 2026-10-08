# VR Driving Simulator with Haptic Feedback

A Unity VR driving simulator developed for my master’s thesis at the University of Stuttgart. The project combines physical driving controls, headset tracking and custom haptic hardware to investigate how feedback affects perceived realism, immersion, comfort and simulator sickness.

![VR driving simulator and hardware setup](vr-driving-simulator.png)

## Project Overview

VR can recreate the visual experience of driving, but conveying acceleration, braking and cornering forces remains a challenge.

This project explores string-based haptic feedback applied through the VR headset as a way to communicate vehicle motion. The simulator integrates a Logitech G923 steering wheel and pedals, an HTC Vive headset and four custom haptic devices into an interactive driving environment.

## What I Developed

- A closed-loop driving simulator in Unity and C#.
- Integration of physical steering and pedal inputs with vehicle control.
- VR camera behaviour using HTC Vive tracking.
- Real-time haptic feedback for acceleration, braking and cornering.
- Collision feedback and configurable feedback conditions.
- A driving route with 22 checkpoints for consistent evaluation.
- A user study comparing two feedback configurations.

## Technology and Hardware

| Area | Tools and components |
|---|---|
| Simulation development | Unity, C# |
| VR hardware | HTC Vive |
| Driving controls | Logitech G923 steering wheel and pedals |
| Haptic feedback | Four string-based devices mounted on the headset |
| Evaluation | Questionnaires, descriptive statistics, Wilcoxon signed-rank tests |

## How the Simulator Works

1. The driver provides steering and pedal inputs through the Logitech G923.
2. The Unity vehicle responds to these inputs within the driving environment.
3. The HTC Vive tracks head movement and provides the immersive view.
4. Vehicle motion is translated into haptic feedback commands.
5. The string-based devices apply directional forces through the headset.
6. Visual and haptic feedback communicate the vehicle’s response to the driver.

## Engineering Work

### Driving Controls and Vehicle Behaviour

Integrated steering and pedal inputs into the simulator and refined vehicle response, steering behaviour and camera feedback to support a consistent driving experience.

### Haptic Hardware Integration

Connected custom haptic devices to the simulation and implemented feedback associated with acceleration, braking and cornering.

This work involved coordinating the visual simulation with physical feedback and investigating how the timing and direction of feedback affected the experience.

### Feedback Timing

Investigated a mismatch between the rendering loop and haptic update rate. Aligning the haptic updates with the rendering loop helped coordinate the physical feedback with the visual experience.

### Test Scenario Development

Created a route with 22 checkpoints to give participants a consistent sequence of driving situations and support comparison between feedback configurations.

## User Study

The simulator was evaluated with **16 participants** across **two configurations**.

The study examined:

- Perceived driving realism.
- Immersion.
- Haptic effectiveness.
- Comfort.
- Simulator sickness.

Questionnaire responses were analysed using descriptive statistics and Wilcoxon signed-rank tests.

## Observations and Lessons

- Head-directed haptic forces introduced comfort challenges.
- Collision feedback was useful for communicating discrete events.
- Camera feedback provided a useful baseline for comparison.
- Timing between visual and physical feedback required careful attention.
- More physical feedback did not automatically produce a better user experience.

The evaluation highlighted the importance of balancing feedback intensity, timing and comfort when designing immersive driving systems.

## Skills Demonstrated

- Unity and C# development.
- Hardware and software integration.
- Real-time interactive simulation.
- VR interaction and camera behaviour.
- Haptic feedback implementation.
- Debugging and iterative testing.
- User study design and execution.
- Statistical comparison and technical documentation.


## Project Scope

This is an academic research prototype. It explores immersive driving feedback and user experience; it is not a validated vehicle dynamics model or a production driver training system.

Reproducing the complete physical experience requires the custom haptic hardware and its associated configuration.

