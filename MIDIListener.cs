<<<<<<< HEAD
﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Devices.Enumeration;
using Windows.Devices.Midi;
using Windows.UI.Xaml;

namespace Stensel
{
    class MIDIListener
    {
        private MidiInPort midiInPort;
        private bool[] channels = new bool[16];
        private long timeout = 2500;
        private Stopwatch stopwatch = new Stopwatch();
        private DispatcherTimer dispatcher = new DispatcherTimer();

        public MIDIListener(MidiInPort midiInPort)
        {
            this.midiInPort = midiInPort;
            this.midiInPort.MessageReceived += MidiInPort_MessageRecieved;
            dispatcher.Interval = new TimeSpan(50);
            dispatcher.Tick += Monitor;
            dispatcher.Start();
        }

        ~MIDIListener()
        {
            midiInPort.MessageReceived -= MidiInPort_MessageRecieved;
            dispatcher.Stop();
        }

        private void Monitor(object sender, object e)
        {
            if(stopwatch.ElapsedMilliseconds > timeout)
            {
                Debug.WriteLine("Timed out.");
                suspendedRecords.AddRange(openControlRecords.Values);
                suspendedRecords.AddRange(openDownRecords.Values);
                openControlRecords.Clear();
                openDownRecords.Clear();
                StoreCurrentRecord();
            }
            if(debouncing && !Debounce())
            {
                StoreCurrentRecord();
            }
        }

        private static Dictionary<int, ChannelRecord> openControlRecords = new Dictionary<int, ChannelRecord>();
        private static Dictionary<byte, ChannelRecord> openDownRecords = new Dictionary<byte, ChannelRecord>();
        private static List<ChannelRecord> suspendedRecords = new List<ChannelRecord>();
        private static Queue<ChannelRecord[]> unfetchedRecords = new Queue<ChannelRecord[]>();
        private byte[] controlKeys = { 81, 83, 84, 86, 88, 89 };
        bool heapInProgress = false;


        public bool HeapInProgress()
        {
            return heapInProgress;
        }

        private void MidiInPort_MessageRecieved(object sender, MidiMessageReceivedEventArgs args)
        {
            stopwatch.Restart();
            IMidiMessage message = args.Message;

            if (message.Type == MidiMessageType.NoteOn)
            {
                MidiNoteOnMessage msg = (MidiNoteOnMessage)message;
                //channels[msg.Channel] = true;
                //DebugChannels();

                if (controlKeys.Contains(msg.Note) && !openControlRecords.ContainsKey(msg.Channel))
                {
                    heapInProgress = true;
                    openControlRecords.Add(msg.Channel, new ChannelRecord(msg.Note));
                }
                else if(!openDownRecords.ContainsKey(msg.Note))
                {
                    heapInProgress = true;
                    openDownRecords.Add(msg.Note, new ChannelRecord(msg.Note));
                }
            }
            else if (message.Type == MidiMessageType.NoteOff)
            {
                MidiNoteOffMessage msg = (MidiNoteOffMessage)message;
                //channels[msg.Channel] = false;
                //DebugChannels();
                
                if (controlKeys.Contains(msg.Note) && openControlRecords.ContainsKey(msg.Channel))
                {
                    suspendedRecords.Add(openControlRecords[msg.Channel]);

                    openControlRecords.Remove(msg.Channel);
                }
                else if (openDownRecords.ContainsKey(msg.Note))
                {
                    suspendedRecords.Add(openDownRecords[msg.Note]);
                    openDownRecords.Remove(msg.Note);
                }

                if (openControlRecords.Count == 0 && openDownRecords.Count == 0)
                {
                    BeginDebounce();
                }
            }
            else if (message.Type == MidiMessageType.ControlChange)
            {
                MidiControlChangeMessage controlMessage = (MidiControlChangeMessage)message;
                int channel = controlMessage.Channel;
                
                if(openControlRecords.ContainsKey(channel))
                {
                    switch (controlMessage.Controller)
                    {
                        case 1:
                            openControlRecords[channel].MakeRecord(controlMessage.ControlValue);
                            //Debug.WriteLine(openRecords[channel].ToString());
                            break;
                        default:
                            Debug.WriteLine("Got unexpected control change on controller " + controlMessage.Controller);
                            break;
                    }
                }
            }
        }

        private static bool debouncing = false;
        private static readonly long debounce = 20;
        private static Stopwatch debounceTimer = new Stopwatch();

        private void BeginDebounce()
        {
            debounceTimer.Restart();
            debouncing = true;
        }

        private bool Debounce()
        {
            if(debounceTimer.ElapsedMilliseconds > debounce && openControlRecords.Count == 0 && openDownRecords.Count == 0)
            {
                debouncing = false;
                debounceTimer.Reset();
                return false;
            }

            return true;
        }

        private void StoreCurrentRecord()
        {
            heapInProgress = false;
            stopwatch.Reset();

            if (suspendedRecords.Count < 1)
                return;

            Debug.WriteLine("\nGrouping input:");
            ChannelRecord[] safeSus = suspendedRecords.ToArray();
            foreach(ChannelRecord ch in safeSus)
            {
                ch.Close();
                Debug.WriteLine("\t" + ch.ToString());
            }

            unfetchedRecords.Enqueue(suspendedRecords.ToArray());

            suspendedRecords.Clear();
        }

        /// <summary>
        /// Get the last set of records that were recorded
        /// </summary>
        /// <returns>null if no last frame to fetch</returns>
        public ChannelRecord[] FetchLastFrame()
        {
            if (openControlRecords.Count < 1 && openDownRecords.Count < 1)
                return null;

            List<ChannelRecord> records = new List<ChannelRecord>(openDownRecords.Values);

            records.AddRange(openControlRecords.Values);

            ChannelRecord[] safe = records.ToArray();
            List<ChannelRecord> copy = new List<ChannelRecord>();
            foreach(var rec in safe)
            {
                copy.Add(new ChannelRecord(rec));
            }

            return copy.ToArray();
        }

        /// <summary>
        /// Get the last set of records that were temporally overlapping
        /// </summary>
        /// <returns>null if no record to fetch</returns>
        public ChannelRecord[] FetchLastHeap()
        {
            if(unfetchedRecords.Count > 0)
            {
                return unfetchedRecords.Dequeue();
            }

            return null;
        }

        private void DebugChannels()
        {
            Debug.Write("\n");
            foreach(bool b in channels)
            {
                if (b)
                    Debug.Write("1");
                else
                    Debug.Write("-");
            }
        }
    }

    public class ChannelRecord
    {
        private List<int> cc = new List<int>();
        private byte note;
        private long downTime;
        private long elapsed;
        private double average = -1;

        public ChannelRecord(ChannelRecord model)
        {
            cc.Add(model.GetLast());
            note = model.GetNote();
        }

        public ChannelRecord(byte note)
        {
            this.note = note;
            downTime = Stopwatch.GetTimestamp();
        }

        public void Close()
        {
            average = GetAverageRecord();
            elapsed = Stopwatch.GetTimestamp() - downTime;
        }

        public void MakeRecord(int x)
        {
            cc.Add(x);
        }

        public byte GetNote()
        {
            return note;
        }

        public int GetLast()
        {
            if(cc.Count > 0)
                return cc.Last();

            return -1;
        }

        public double GetAverage()
        {
            return average;
        }

        public long GetElapsed()
        {
            return elapsed;
        }

        private double GetAverageRecord()
        {
            if(cc.Count > 0)
                return cc.Average();

            return -1;
        }

        public override string ToString()
        {
            if(elapsed > 0)
                return "\t## NOTE: " + note + "\t## Average change:" + average;

            return "##NOTE: " + note + "\t##Latest change:" + GetLast();
        }
    }
}
=======
﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Devices.Enumeration;
using Windows.Devices.Midi;
using Windows.UI.Xaml;

namespace Stensel
{
    class MIDIListener
    {
        private MidiInPort midiInPort;
        private bool[] channels = new bool[16];
        private long timeout = 2500;
        private Stopwatch stopwatch = new Stopwatch();
        private DispatcherTimer dispatcher = new DispatcherTimer();

        public MIDIListener(MidiInPort midiInPort)
        {
            this.midiInPort = midiInPort;
            this.midiInPort.MessageReceived += MidiInPort_MessageRecieved;
            dispatcher.Interval = new TimeSpan(50);
            dispatcher.Tick += Monitor;
            dispatcher.Start();
        }

        ~MIDIListener()
        {
            midiInPort.MessageReceived -= MidiInPort_MessageRecieved;
            dispatcher.Stop();
        }

        private void Monitor(object sender, object e)
        {
            if(stopwatch.ElapsedMilliseconds > timeout)
            {
                Debug.WriteLine("Timed out.");
                suspendedRecords.AddRange(openControlRecords.Values);
                suspendedRecords.AddRange(openDownRecords.Values);
                openControlRecords.Clear();
                openDownRecords.Clear();
                StoreCurrentRecord();
            }
            if(debouncing && !Debounce())
            {
                StoreCurrentRecord();
            }
        }

        private static Dictionary<int, ChannelRecord> openControlRecords = new Dictionary<int, ChannelRecord>();
        private static Dictionary<byte, ChannelRecord> openDownRecords = new Dictionary<byte, ChannelRecord>();
        private static List<ChannelRecord> suspendedRecords = new List<ChannelRecord>();
        private static Queue<ChannelRecord[]> unfetchedRecords = new Queue<ChannelRecord[]>();
        private byte[] controlKeys = { 81, 83, 84, 86, 88, 89 };
        bool heapInProgress = false;


        public bool HeapInProgress()
        {
            return heapInProgress;
        }

        private void MidiInPort_MessageRecieved(object sender, MidiMessageReceivedEventArgs args)
        {
            stopwatch.Restart();
            IMidiMessage message = args.Message;

            if (message.Type == MidiMessageType.NoteOn)
            {
                MidiNoteOnMessage msg = (MidiNoteOnMessage)message;
                //channels[msg.Channel] = true;
                //DebugChannels();

                if (controlKeys.Contains(msg.Note) && !openControlRecords.ContainsKey(msg.Channel))
                {
                    heapInProgress = true;
                    openControlRecords.Add(msg.Channel, new ChannelRecord(msg.Note));
                }
                else if(!openDownRecords.ContainsKey(msg.Note))
                {
                    heapInProgress = true;
                    openDownRecords.Add(msg.Note, new ChannelRecord(msg.Note));
                }
            }
            else if (message.Type == MidiMessageType.NoteOff)
            {
                MidiNoteOffMessage msg = (MidiNoteOffMessage)message;
                //channels[msg.Channel] = false;
                //DebugChannels();
                
                if (controlKeys.Contains(msg.Note) && openControlRecords.ContainsKey(msg.Channel))
                {
                    suspendedRecords.Add(openControlRecords[msg.Channel]);

                    openControlRecords.Remove(msg.Channel);
                }
                else if (openDownRecords.ContainsKey(msg.Note))
                {
                    suspendedRecords.Add(openDownRecords[msg.Note]);
                    openDownRecords.Remove(msg.Note);
                }

                if (openControlRecords.Count == 0 && openDownRecords.Count == 0)
                {
                    BeginDebounce();
                }
            }
            else if (message.Type == MidiMessageType.ControlChange)
            {
                MidiControlChangeMessage controlMessage = (MidiControlChangeMessage)message;
                int channel = controlMessage.Channel;
                
                if(openControlRecords.ContainsKey(channel))
                {
                    switch (controlMessage.Controller)
                    {
                        case 1:
                            openControlRecords[channel].MakeRecord(controlMessage.ControlValue);
                            //Debug.WriteLine(openRecords[channel].ToString());
                            break;
                        default:
                            Debug.WriteLine("Got unexpected control change on controller " + controlMessage.Controller);
                            break;
                    }
                }
            }
        }

        private static bool debouncing = false;
        private static readonly long debounce = 20;
        private static Stopwatch debounceTimer = new Stopwatch();

        private void BeginDebounce()
        {
            debounceTimer.Restart();
            debouncing = true;
        }

        private bool Debounce()
        {
            if(debounceTimer.ElapsedMilliseconds > debounce && openControlRecords.Count == 0 && openDownRecords.Count == 0)
            {
                debouncing = false;
                debounceTimer.Reset();
                return false;
            }

            return true;
        }

        private void StoreCurrentRecord()
        {
            heapInProgress = false;
            stopwatch.Reset();

            if (suspendedRecords.Count < 1)
                return;

            Debug.WriteLine("\nGrouping input:");
            ChannelRecord[] safeSus = suspendedRecords.ToArray();
            foreach(ChannelRecord ch in safeSus)
            {
                ch.Close();
                Debug.WriteLine("\t" + ch.ToString());
            }

            unfetchedRecords.Enqueue(suspendedRecords.ToArray());

            suspendedRecords.Clear();
        }

        /// <summary>
        /// Get the last set of records that were recorded
        /// </summary>
        /// <returns>null if no last frame to fetch</returns>
        public ChannelRecord[] FetchLastFrame()
        {
            if (openControlRecords.Count < 1 && openDownRecords.Count < 1)
                return null;

            List<ChannelRecord> records = new List<ChannelRecord>(openDownRecords.Values);

            records.AddRange(openControlRecords.Values);

            ChannelRecord[] safe = records.ToArray();
            List<ChannelRecord> copy = new List<ChannelRecord>();
            foreach(var rec in safe)
            {
                copy.Add(new ChannelRecord(rec));
            }

            return copy.ToArray();
        }

        /// <summary>
        /// Get the last set of records that were temporally overlapping
        /// </summary>
        /// <returns>null if no record to fetch</returns>
        public ChannelRecord[] FetchLastHeap()
        {
            if(unfetchedRecords.Count > 0)
            {
                return unfetchedRecords.Dequeue();
            }

            return null;
        }

        private void DebugChannels()
        {
            Debug.Write("\n");
            foreach(bool b in channels)
            {
                if (b)
                    Debug.Write("1");
                else
                    Debug.Write("-");
            }
        }
    }

    public class ChannelRecord
    {
        private List<int> cc = new List<int>();
        private byte note;
        private long downTime;
        private long elapsed;
        private double average = -1;

        public ChannelRecord(ChannelRecord model)
        {
            cc.Add(model.GetLast());
            note = model.GetNote();
        }

        public ChannelRecord(byte note)
        {
            this.note = note;
            downTime = Stopwatch.GetTimestamp();
        }

        public void Close()
        {
            average = GetAverageRecord();
            elapsed = Stopwatch.GetTimestamp() - downTime;
        }

        public void MakeRecord(int x)
        {
            cc.Add(x);
        }

        public byte GetNote()
        {
            return note;
        }

        public int GetLast()
        {
            if(cc.Count > 0)
                return cc.Last();

            return -1;
        }

        public double GetAverage()
        {
            return average;
        }

        public long GetElapsed()
        {
            return elapsed;
        }

        private double GetAverageRecord()
        {
            if(cc.Count > 0)
                return cc.Average();

            return -1;
        }

        public override string ToString()
        {
            if(elapsed > 0)
                return "\t## NOTE: " + note + "\t## Average change:" + average;

            return "##NOTE: " + note + "\t##Latest change:" + GetLast();
        }
    }
}
>>>>>>> c556179d1162fb6a947d60fd7209c59bed5ad916
