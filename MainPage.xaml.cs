using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Windows.Devices.Enumeration;
using Windows.Devices.Midi;
using System.Threading.Tasks;
using Windows.UI.Xaml.Shapes;

namespace Stensel
{
    /// <summary>
    /// The main window
    /// </summary>
    public sealed partial class MainPage : Page
    {
        MIDIDeviceWatcher deviceInFinder;
        MIDIDeviceWatcher deviceOutFinder;
        MIDIListener listener;
        MIDITransmitter transmitter;
        Layout lo;
        DispatcherTimer uiDrawClock = new DispatcherTimer();

        public MainPage()
        {
            InitializeComponent();
            Background = new SolidColorBrush(Windows.UI.Colors.LightGray);
            // Keep up to date with connected MIDI devices
            deviceInFinder = new MIDIDeviceWatcher(MidiInPort.GetDeviceSelector(), midiInPortListBox, Dispatcher);
            deviceInFinder.StartWatcher();
            deviceOutFinder = new MIDIDeviceWatcher(MidiOutPort.GetDeviceSelector(), midiOutPortListBox, Dispatcher);
            deviceOutFinder.StartWatcher();

            DrawLayout();
            uiDrawClock.Tick += ShowCurChord;
            uiDrawClock.Interval = new TimeSpan(20);
        }

        #region Graphics
        Dictionary<string, Rectangle> layout = new Dictionary<string, Rectangle>();

        private async void DrawLayout()
        {
            lo = new Layout();
            var keys = await lo.LoadLayout();

            foreach (var lkey in keys)
            {
                var key = new Rectangle();
                key.Width = 36;
                key.Height = 36;
                key.Fill = new SolidColorBrush(Windows.UI.Colors.White);
                key.SetValue(Canvas.LeftProperty, 38 * lkey.xPos + 5);
                key.SetValue(Canvas.TopProperty, 38 * lkey.yPos);
                StenoLayout.Children.Add(key);
                layout.Add(lkey.name, key);

                var text = new TextBlock();
                text.Width = 38;
                text.Height = 38;
                text.TextAlignment = TextAlignment.Center;
                text.Text = lkey.GetDisplayName();
                text.SetValue(Canvas.LeftProperty, 38 * lkey.xPos + 5);
                text.SetValue(Canvas.TopProperty, 38 * lkey.yPos);
                StenoLayout.Children.Add(text);
            }
        }

        private void ShowCurChord(object sender, object e)
        {
            if (listener != null)
            {
                var held = listener.FetchLastFrame();

                if (held != null)
                {
                    var notes = lo.GetActiveNoteNames(held);

                    foreach (string name in notes)
                    {
                        layout[name].Fill = new SolidColorBrush(Windows.UI.Colors.SteelBlue);
                    }
                }
                else
                {
                    foreach (var key in layout.Values)
                    {
                        key.Fill = new SolidColorBrush(Windows.UI.Colors.White);
                    }
                }


                if(transmitter != null)
                {
                    var lastHeap = listener.FetchLastHeap();

                    if (lastHeap != null)
                    {
                        byte[] sendNotes = lo.GetActiveNotes(lastHeap);
                        transmitter.Transmit(sendNotes);
                    }
                }
            }
        }
        #endregion

        /// <summary>
        /// Called when MIDI input device is selected
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void midiInPortListBox_SelectionChanged(object sender, SelectionChangedEventArgs args)
        {
            var devicesInfo = deviceInFinder.DeviceInformationCollection;
            if (devicesInfo == null)
            {
                Debug.WriteLine("Device finder could not locate any device information.");
                return;
            }

            DeviceInformation deviceInfo = devicesInfo[midiInPortListBox.SelectedIndex];

            if (deviceInfo == null)
            {
                Debug.WriteLine("Could not locate device information for selected device.");
                return;
            }

            Debug.WriteLine("Attempting to listen to device #" + midiInPortListBox.SelectedIndex);

            await SetListener(deviceInfo.Id);
        }

        private async void midiOutPortListBox_SelectionChanged(object sender, SelectionChangedEventArgs args)
        {
            var devicesInfo = deviceOutFinder.DeviceInformationCollection;
            if (devicesInfo == null)
            {
                Debug.WriteLine("Device finder could not locate any device information.");
                return;
            }

            DeviceInformation deviceInfo = devicesInfo[midiOutPortListBox.SelectedIndex];

            if (deviceInfo == null)
            {
                Debug.WriteLine("Could not locate device information for selected device.");
                return;
            }

            Debug.WriteLine("Attempting to transmit to device #" + midiOutPortListBox.SelectedIndex);

            await SetTransmitter(deviceInfo.Id);
        }

        /// <summary>
        /// Passes a MIDI input port to the MIDIListener
        /// </summary>
        /// <param name="portID">the port that is passed</param>
        /// <returns>task</returns>
        private async Task SetListener(string portID)
        {
            MidiInPort listening = await MidiInPort.FromIdAsync(portID);

            if (listening == null)
            {
                Debug.WriteLine("Unable to create MIDI-in port.");
                return;
            }

            listener = new MIDIListener(listening);
            uiDrawClock.Start();
        }

        /// <summary>
        /// Passes a MIDI output port to the MIDITransmitter
        /// </summary>
        /// <param name="portID">the port that is passed</param>
        /// <returns>task</returns>
        private async Task SetTransmitter(string portID)
        {
            IMidiOutPort transmitting = await MidiOutPort.FromIdAsync(portID);

            if (transmitting == null)
            {
                Debug.WriteLine("Unable to create MIDI-out port.");
                return;
            }

            transmitter = new MIDITransmitter(transmitting);
        }
    }
}
