# Stensel
MIDI filter, translator and tunnel that enables the Sensel Morph to be used for stenography with plover.

UWP app, only supports windows 10. This is in a proof-of-concept state and will crash. There are tons of things that need tuning, including but not limited to: Debouncing, timing out, handling devices being removed etc. Not to mention that the UI could use some polish.


# How to use
The sensel morph you are using will have to be set up using the sensel app. See map.png for an example. Assets/Stenotype.txt contains mappings of which MIDI-notes correspond to which keys on the steno machine.

The program has two drop down boxes. The left is input and the right is output. Select your Morph as input. You'll need a MIDI port to output to that can be seen by plover (you'll need to use the plover-midi plugin!). As of right now, Stensel cannot create a virtual output port. LoopMIDI is a great piece of software that allows you to create virtual MIDI ports to route messages through (https://www.tobias-erichsen.de/software/loopmidi.html).
