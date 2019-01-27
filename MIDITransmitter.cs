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
        private Queue<byte[]> transmitted = new Queue<byte[]>();

        public MIDITransmitter(IMidiOutPort midiOutPort)
        {
            this.midiOutPort = midiOutPort;
            dispatcher.Tick += SendNotesOff;
            dispatcher.Interval = new TimeSpan(50);
            dispatcher.Start();
        }

        public void Transmit(byte[] notes)
        {
            foreach(byte note in notes)
            {
                midiOutPort.SendMessage(new MidiNoteOnMessage(0, note, 127));
            }

            transmitted.Enqueue(notes);
        }

        private void SendNotesOff(object sender, object e)
        {
            if(transmitted.Count > 0)
            {
                if (stopwatch.ElapsedMilliseconds > noteOpenTime)
                {
                    byte[] notes = transmitted.Dequeue();
                    foreach (byte note in notes)
                        midiOutPort.SendMessage(new MidiNoteOffMessage(0, note, 127));

                    if (transmitted.Count == 0)
                        stopwatch.Stop();
                    else
                        stopwatch.Restart();
                }
                else if (!stopwatch.IsRunning)
                {
                    stopwatch.Restart();
                }
            }
        }
    }
}
