using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;

namespace Stensel
{
    class Layout
    {
        public struct LayoutKey
        {
            public LayoutKey(int xPos, int yPos, string name)
            {
                this.xPos = xPos;
                this.yPos = yPos;
                this.name = name;
            }

            public int xPos;
            public int yPos;
            public string name;

            public string GetDisplayName()
            {
                return name.Replace("-", "");
            }
        }

        private Dictionary<byte, string[][]> controlSurfaces = new Dictionary<byte, string[][]>();
        private Dictionary<byte, string[]> noteSurfaces = new Dictionary<byte, string[]>();
        private Dictionary<string, byte> namesToNotes = new Dictionary<string, byte>();

        public async Task<LayoutKey[]> LoadLayout()
        {
            StorageFolder sfold = ApplicationData.Current.LocalFolder;
            StorageFile sf = (StorageFile) await sfold.TryGetItemAsync("Stenotype.txt");

            if (sf == null)
            {
                // Copy the file from the install folder to the local folder
                var folder = await Windows.ApplicationModel.Package.Current.InstalledLocation.GetFolderAsync("Assets");

                var file = await folder.GetFileAsync("Stenotype.txt");
                if (file != null)
                {
                    await file.CopyAsync(sfold, "Stenotype.txt", NameCollisionOption.FailIfExists);
                }

                sf = await sfold.GetFileAsync("Stenotype.txt");
            }


            var fs = File.Open(sf.Path, FileMode.Open);
            TextReader tr = new StreamReader(fs);

            List<LayoutKey> ret = new List<LayoutKey>();

            string line;

            while ((line = tr.ReadLine()) != null)
            {
                if (line.Contains("---"))
                    break;

                if (line.Length > 4 && line[0] != '\'')
                {
                    string[] parts = line.Split(';');

                    if (parts.Length > 2)
                        ret.Add(new LayoutKey(int.Parse(parts[1]), int.Parse(parts[2]), parts[0]));
                }
            }

            // While the list to be returned is already completed, we'll take this moment to read the rest of the data as well
            while ((line = tr.ReadLine()) != null)
            {
                if (line.Contains("---"))
                    break;

                if (line.Length > 2 && line[0] != '\'')
                {
                    string[] parts = line.Split(';');

                    if (parts.Length > 1)
                    {
                        string[] keys = parts[1].Split(',');
                        noteSurfaces.Add(byte.Parse(parts[0]), keys);

                        if(parts.Length == 2)
                        {
                            if(!namesToNotes.ContainsKey(parts[1]))
                            {
                                namesToNotes.Add(parts[1], byte.Parse(parts[0]));
                            }
                        }
                    }
                }
            }

            while ((line = tr.ReadLine()) != null)
            {
                if (line.Contains("---"))
                    break;

                if (line.Length > 0 && line[0] == '!')
                {
                    string key = line.Substring(1);
                    if(key.Length > 0)
                    {
                        byte note = byte.Parse(key);

                        string[][] noteNames = new string[128][];
                        string subline;
                        int index = 0;

                        while ((subline = tr.ReadLine()) != null)
                        {

                            if(subline.Length > 0)
                            {
                                if (subline[0] == '?')
                                    break;

                                string[] parts = subline.Split(';');

                                if (parts.Length > 1)
                                {
                                    string[] keys = parts[1].Split(',');

                                    int upto = int.Parse(parts[0]);

                                    while (index <= upto)
                                        noteNames[index++] = keys;
                                }
                            }
                        }

                        controlSurfaces.Add(note, noteNames);
                    }
                }
            }

            return ret.ToArray();
        }

        public string[] GetActiveNoteNames(ChannelRecord[] records)
        {
            List<string> ret = new List<string>();

            foreach(var rec in records)
            {
                if (noteSurfaces.ContainsKey(rec.GetNote()))
                {
                    foreach(string note in noteSurfaces[rec.GetNote()])
                    {
                        if(!ret.Contains(note))
                        {
                            ret.Add(note);
                        }
                    }
                }
                else if(controlSurfaces.ContainsKey(rec.GetNote()) && rec.GetLast() > -1)
                {
                    foreach (string note in controlSurfaces[rec.GetNote()][rec.GetLast()])
                    {
                        if (!ret.Contains(note))
                        {
                            ret.Add(note);
                        }
                    }
                }
            }

            return ret.ToArray();
        }

        public byte[] GetActiveNotes(ChannelRecord[] records)
        {
            string[] noteNames = GetActiveNoteNames(records);
            List<byte> ret = new List<byte>();

            foreach(string noteName in noteNames)
            {
                if (namesToNotes.ContainsKey(noteName) && !ret.Contains(namesToNotes[noteName]))
                    ret.Add(namesToNotes[noteName]);
            }

            return ret.ToArray();
        }
    }
}
