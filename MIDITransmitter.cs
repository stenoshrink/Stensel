using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Devices.Midi;
using Windows.UI.Xaml;

namespace Stensel
{
    class MIDITransmitter
    {
        private IMidiOutPort midiOutPort;
        private Stopwatch stopwatch = new Stopwatch();
        private long noteOpenTime = 10;
        private DispatcherTimer dispatcher = new DispatcherTimer();
        private Queue<byte[]> transmit = new Queue<byte[]>();
        private Queue<byte> transmitted = new Queue<byte>();

        public MIDITransmitter(IMidiOutPort midiOutPort)
        {
            this.midiOutPort = midiOutPort;
            dispatcher.Tick += TransmitTick;
            dispatcher.Interval = new TimeSpan(20);
            dispatcher.Start();
        }

        public void Transmit(byte[] notes)
        {
            if(transmitted.Count == 0)
            {
                if(transmit.Count > 0)
                {
                    byte[] prefer = transmit.Dequeue();

                    foreach (byte note in prefer)
                    {
                        midiOutPort.SendMessage(new MidiNoteOnMessage(0, note, 127));
                        transmitted.Enqueue(note);
                    }

                    transmit.Enqueue(notes);
                }
                else
                {
                    foreach (byte note in notes)
                    {
                        midiOutPort.SendMessage(new MidiNoteOnMessage(0, note, 127));
                        transmitted.Enqueue(note);
                    }
                }
            }
            else // Wait upon closing the transmitted notes
            {
                transmit.Enqueue(notes);
            }
        }

        private void TransmitTick(object sender, object e)
        {
            if(transmitted.Count > 0)
            {
                if (stopwatch.ElapsedMilliseconds > noteOpenTime)
                {
                    while(transmitted.Count > 0)
                        midiOutPort.SendMessage(new MidiNoteOffMessage(0, transmitted.Dequeue(), 127));

                    stopwatch.Stop();
                }
                else if (!stopwatch.IsRunning)
                {
                    stopwatch.Restart();
                }
            }
            else if(transmit.Count > 0)
            {
                Transmit(transmit.Dequeue());
            }
        }
    }
}
