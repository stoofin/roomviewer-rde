using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using System.IO;
using System.IO.Compression;
using System.Globalization;
using System.Collections;
using System.Drawing.Imaging;
using System.Linq;
using System.Security.Cryptography;
using System.Windows;
using System.Diagnostics;

namespace viewer
{

    public partial class Form1 : Form
    {
        private Button exportButton;
        private Button exportAllButton;
        private Button importButton;
        private Button batchConvertButton;
        private Button saveChip2Button;
        private CheckBox hdCheckBox;
        private CheckBox cropExportedLayersCheckBox;
        private Label hdCheckBoxLabel;
        private Label cropExportedLayersLabel;
        private FolderBrowserDialog folderImportDialog;
        private FolderBrowserDialog folderExportDialog;
        private SaveFileDialog saveChip2Dialog;
        private FolderBrowserDialog saveBatchChip2Dialog;
        private ComboBox roomComboBox;
        private CheckBox zoomPreviewCheckBox;
        private Label zoomPreviewCheckBoxLabel;

        private Panel imageScrollPanel;
        public Form1()
        {
            InitializeComponent();
            roomComboBox = new ComboBox();
            roomComboBox.Anchor = AnchorStyles.None;
            roomComboBox.Name = "roomComboBox";
            roomComboBox.TabIndex = 3;
            roomComboBox.AutoSize = true;
            roomComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
            roomComboBox.DropDownHeight = 600;
            roomComboBox.DropDownWidth = 150;
            roomComboBox.SelectedIndexChanged += new EventHandler(roomComboBox_SelectedIndexChanged);
            toolsPanel.Controls.Add(roomComboBox);

            saveChip2Dialog = new SaveFileDialog();
            saveBatchChip2Dialog = new FolderBrowserDialog();
            saveBatchChip2Dialog.Description = "Choose directory to output chip2 files to";
            folderImportDialog = new FolderBrowserDialog();
            folderImportDialog.Description = "Choose directory to import png files from";
            folderExportDialog = new FolderBrowserDialog();
            folderExportDialog.Description = "Choose directory to export png files to";

            hdCheckBoxLabel = new Label();
            hdCheckBoxLabel.Anchor = AnchorStyles.None;
            hdCheckBoxLabel.AutoSize = true;
            hdCheckBoxLabel.Name = "hdCheckBoxLabel";
            hdCheckBoxLabel.Text = "HD";
            toolsPanel.Controls.Add(hdCheckBoxLabel);
            
            hdCheckBox = new CheckBox();
            hdCheckBox.Anchor = AnchorStyles.None;
            hdCheckBox.AutoSize = true;
            hdCheckBox.Name = "hdCheckBox";
            hdCheckBox.CheckedChanged += new EventHandler(hdCheckBox_ValueChanged);
            toolsPanel.Controls.Add(hdCheckBox);

            zoomPreviewCheckBoxLabel = new Label();
            zoomPreviewCheckBoxLabel.Anchor = AnchorStyles.None;
            zoomPreviewCheckBoxLabel.AutoSize = true;
            zoomPreviewCheckBoxLabel.Name = "zoomPreviewCheckBoxLabel";
            zoomPreviewCheckBoxLabel.Text = "Zoom";
            toolsPanel.Controls.Add(zoomPreviewCheckBoxLabel);

            zoomPreviewCheckBox = new CheckBox();
            zoomPreviewCheckBox.Anchor = AnchorStyles.None;
            zoomPreviewCheckBox.AutoSize = true;
            zoomPreviewCheckBox.Name = "zoomPreviewCheckBox";
            zoomPreviewCheckBox.CheckedChanged += new EventHandler(zoomPreviewCheckBox_ValueChanged);
            toolsPanel.Controls.Add(zoomPreviewCheckBox);

            exportButton = new Button();
            exportButton.AutoSize = true;
            exportButton.Text = "Export Room";
            exportButton.Click += new EventHandler(OnExportClick);
            toolsPanel.Controls.Add(exportButton);

            exportAllButton = new Button();
            exportAllButton.AutoSize = true;
            exportAllButton.Text = "Export All Rooms";
            exportAllButton.Click += new EventHandler(OnExportAllClick);
            toolsPanel.Controls.Add(exportAllButton);

            cropExportedLayersLabel = new Label();
            cropExportedLayersLabel.Anchor = AnchorStyles.None;
            cropExportedLayersLabel.AutoSize = true;
            cropExportedLayersLabel.Name = "cropExportedLayersLabel";
            cropExportedLayersLabel.Text = "Crop Exported Layers";
            toolsPanel.Controls.Add(cropExportedLayersLabel);

            cropExportedLayersCheckBox = new CheckBox();
            cropExportedLayersCheckBox.Anchor = AnchorStyles.None;
            cropExportedLayersCheckBox.AutoSize = true;
            cropExportedLayersCheckBox.Name = "cropExportedLayersCheckBox";
            // cropExportedLayersCheckBox.Checked = true;
            toolsPanel.Controls.Add(cropExportedLayersCheckBox);

            importButton = new Button();
            importButton.AutoSize = true;
            importButton.Text = "Import HD(x4) Room";
            importButton.Click += new EventHandler(OnImportClick);
            toolsPanel.Controls.Add(importButton);

            saveChip2Button = new Button();
            saveChip2Button.AutoSize = true;
            saveChip2Button.Text = "Save HD as .chip2";
            saveChip2Button.Click += new EventHandler(OnSaveChip2Click);
            toolsPanel.Controls.Add(saveChip2Button);

            batchConvertButton = new Button();
            batchConvertButton.AutoSize = true;
            batchConvertButton.Text = "Batch Convert to .chip2";
            batchConvertButton.Click += new EventHandler(OnBatchConvertClick);
            toolsPanel.Controls.Add(batchConvertButton);

            imageScrollPanel = new Panel();
            imageScrollPanel.Name = "imageScrollPanel";
            imageScrollPanel.TabIndex = 1;
            imageScrollPanel.TabStop = false;
            imageScrollPanel.AutoSize = true;
            imageScrollPanel.AutoScroll = true;
            imageScrollPanel.Dock = DockStyle.Fill;
            Controls.Add(imageScrollPanel);
            imageScrollPanel.BringToFront();

            pictureBox1.SizeMode = PictureBoxSizeMode.Zoom;
            pictureBox1.Location = new Point(0, 0);
            imageScrollPanel.Controls.Add(pictureBox1);

            Resize += new EventHandler(OnResize);
        }

        struct TileCmd
        {
            public int x;
            public int y;
            public int z;
            public int order;
            public int mode;
            public int u;
            public int v;
            public int p;

            public int tileX;
            public int tileY;
            public int paletteIndex;
        }


        int CompareByZ(Layer l1, Layer l2)
        {
            if (l1.CmdList[0].z > l2.CmdList[0].z) return -1;
            if (l1.CmdList[0].z < l2.CmdList[0].z) return 1;
            return 0;
        }
        
        struct Layer
        {
            public int Index;
            public Rectangle Bounds;
            public TileCmd[] CmdList;
        }

        struct MeshVertex
        {
            public short X;
            public short Y;
            public short Z;
            public short W;
        }

        struct MeshTriangle
        {
            public short[] Vertices;
            public short[] Info;
        }

        int total_rooms = 537;
        int loaded_room = -1;
        int loaded_room_highest_palette = 0;
        List<string> roomNames = new List<string>();
        string loaded_room_name = "No Room";
        byte[] vramdata = new byte[2048 * 512];
        short[] hd_data;
        short[] layerbuffer;
        bool show_hd = false;

        Layer[] layers;
        MeshVertex[] vertices;
        MeshTriangle[] triangles;
        CheckBox[] cbgroup;
        string lastdir = string.Empty;
        string isopath = string.Empty;

        Rectangle roomBounds;

        private void ReadCTD(Stream fs)
        {
            BufferedStream bs = new BufferedStream(fs, 32768);
            // BinaryReader has no Seek method, but can still seek the underlying stream
            // https://stackoverflow.com/questions/19134172/is-it-safe-to-use-stream-seek-when-a-binaryreader-is-open
            BinaryReader reader = new BinaryReader(bs);
            int gfxsections = reader.ReadInt32();
            if (gfxsections != 0x73277449) //It's CDMAKE Dummy
            {
                for (int i = 0; i < gfxsections; i++)
                {
                    bs.Seek(4 + 4 * i, SeekOrigin.Begin);
                    int begin = reader.ReadInt32();
                    int end = reader.ReadInt32();

                    //now read first 8 bytes to check if it is a tim16 header
                    bs.Seek(begin, SeekOrigin.Begin);
                    if (reader.ReadInt32() == 0x10 && reader.ReadInt32() == 1)
                    {
                        bs.Seek(begin + 0xcL, SeekOrigin.Begin);
                        ushort x = reader.ReadUInt16();
                        ushort y = reader.ReadUInt16();
                        ushort width = reader.ReadUInt16();
                        ushort height = reader.ReadUInt16();
                        for (int h = 0; h < height; h++)
                        {
                            for (int w = 0; w < width * 2; w++)
                            {
                                byte read = (byte)reader.ReadByte();
                                vramdata[w + x * 2 + (h + y) * 2048] = read;
                            }
                        }
                    }
                    else continue;

                }
            }
        }
        private void ReadBIN(Stream fs)
        {
            BufferedStream bs = new BufferedStream(fs, 32768);
            BinaryReader reader = new BinaryReader(bs);
            //MessageBox.Show("" + (DateTime.Now - ddd));
            int codesections = reader.ReadInt32();
            if (codesections != 0x73277449) //It's CDMAKE Dummy
            {
                bs.Seek(0 + 8, SeekOrigin.Begin); //section 1 is palette
                int begin = (int)0 + reader.ReadInt32();
                int end = (int)0 + reader.ReadInt32();
                bs.Seek(begin, SeekOrigin.Begin);
                for (int p = 0; p < (end - begin) / 512; p++)
                    reader.Read(vramdata, 0x70000 + 2048 * p, 512);
            }
            /*End Construct VRAM*/

            layers = null;
            layerbuffer = null;
            /*
                *Construct Layers
                */
            if (codesections != 0x73277449) //It's CDMAKE Dummy
            {
                bs.Seek(0 + 4 + 4 * 7, SeekOrigin.Begin); //section 7 is layers
                int begin = (int)0 + reader.ReadInt32();
                int end = (int)0 + reader.ReadInt32();
                bs.Seek(begin + 4, SeekOrigin.Begin);
                int outlen = reader.ReadInt32();
                byte[] lzssbuffer = new byte[end - begin];
                byte[] outbuffer = new byte[outlen];
                bs.Seek(begin, SeekOrigin.Begin);
                reader.Read(lzssbuffer, 0, lzssbuffer.Length);
                int unknown;
                //now, decompress it
                using (BitStream in_f = new BitStream(new MemoryStream(lzssbuffer), false))
                {
                    using (MemoryStream msout = new MemoryStream(outbuffer))
                    {
                        Lzss.Decompress(in_f, msout, out unknown);
                    }
                }
                using (BinaryReader br = new BinaryReader(new MemoryStream(outbuffer)))
                {
                    int count = br.ReadInt32(); // how many layers?
                    layers = new Layer[count];
                    uint[] offsets = new uint[count + 1];
                    offsets[0] = br.ReadUInt32(); //zeros?
                    for (int t = 1; t <= count; t++)
                    {
                        offsets[t] = br.ReadUInt32();
                    }
                    //calculate crop area
                    /*
                    int minx = 9999999, miny = 9999999, maxx = -9999999, maxy = -9999999;
                    for (int li = 0; li < count; li++) // layers
                    {
                        for (int i = 0; i < offsets[li + 1] - offsets[li]; i++)
                        {
                            short x = br.ReadInt16();
                            short y = br.ReadInt16();
                            if (x < minx) minx = x;
                            if (x > maxx) maxx = x;
                            if (y < miny) miny = y;
                            if (y > maxy) maxy = y;
                            br.BaseStream.Seek(8, SeekOrigin.Current);
                        }
                    }
                    layerbuffer = new short[(maxx - minx + 16) * (maxy - miny + 16)];
                        */

                    //rewind
                    br.BaseStream.Seek(8 + count * 4, SeekOrigin.Begin);

                    loaded_room_highest_palette = 0;
                    roomBounds = new Rectangle(0, 0, 0, 0);
                    for (int li = 0; li < count; li++) // layers
                    {
                        layers[li].Index = li;
                        int tc = (int)(offsets[li + 1] - offsets[li]);
                        layers[li].CmdList = new TileCmd[tc];
                        int minx = 9999999, miny = 9999999, maxx = -9999999, maxy = -9999999;
                        for (int i = 0; i < tc; i++)
                        {
                            short x = br.ReadInt16();
                            short y = br.ReadInt16();
                            if (x < minx) minx = x;
                            if (x > maxx) maxx = x;
                            if (y < miny) miny = y;
                            if (y > maxy) maxy = y;
                            layers[li].CmdList[i].x = x;
                            layers[li].CmdList[i].y = y;
                            int u = (br.ReadByte() | (br.ReadByte() << 8));
                            int v = br.ReadByte();
                            int p = br.ReadByte();
                            layers[li].CmdList[i].tileX = u;
                            layers[li].CmdList[i].tileY = v;
                            layers[li].CmdList[i].paletteIndex = p;
                            loaded_room_highest_palette = Math.Max(loaded_room_highest_palette, p);
                            layers[li].CmdList[i].u = u << 1;
                            layers[li].CmdList[i].v = v + 256;
                            layers[li].CmdList[i].p = 0x70000 + (p << 8);
                            layers[li].CmdList[i].order = br.ReadByte();
                            layers[li].CmdList[i].mode = br.ReadByte();
                            layers[li].CmdList[i].z = br.ReadUInt16();
                        }
                        layers[li].Bounds = new Rectangle(minx, miny, maxx - minx + 16, maxy - miny + 16);
                        if (li == 0) {
                            roomBounds = layers[li].Bounds;
                        } else {
                            roomBounds = Rectangle.Union(roomBounds, layers[li].Bounds);
                        }
                    }
                }
                bs.Seek(0 + 4 + 4 * 4, SeekOrigin.Begin); //section 4 is walk mesh triangles
                begin = (int)0 + reader.ReadInt32();
                end = (int)0 + reader.ReadInt32();
                byte[] tribuf = new byte[end - begin];
                bs.Seek(begin, SeekOrigin.Begin);
                reader.Read(tribuf, 0, tribuf.Length);
                using (BinaryReader br = new BinaryReader(new MemoryStream(tribuf)))
                {
                    int tricount = br.ReadInt32();
                    triangles = new MeshTriangle[tricount];
                    for (int t = 0; t < tricount; t++)
                    {
                        triangles[t].Vertices = new short[3];
                        triangles[t].Vertices[0] = br.ReadInt16();
                        triangles[t].Vertices[1] = br.ReadInt16();
                        triangles[t].Vertices[2] = br.ReadInt16();
                        triangles[t].Info = new short[4];
                        triangles[t].Info[0] = br.ReadInt16();
                        triangles[t].Info[1] = br.ReadInt16();
                        triangles[t].Info[2] = br.ReadInt16();
                        triangles[t].Info[3] = br.ReadInt16();
                    }
                }
                bs.Seek(0 + 4 + 4 * 5, SeekOrigin.Begin); //section 5 is vertex buffer
                begin = (int)0 + reader.ReadInt32();
                end = (int)0 + reader.ReadInt32();
                byte[] vbuf = new byte[end - begin];
                bs.Seek(begin, SeekOrigin.Begin);
                reader.Read(vbuf, 0, vbuf.Length);
                using (BinaryReader br = new BinaryReader(new MemoryStream(vbuf)))
                {
                    int vcount = br.ReadInt32() / 8;
                    vertices = new MeshVertex[vcount];
                    for (int t = 0; t < vcount; t++)
                    {
                        vertices[t].X = br.ReadInt16();
                        vertices[t].Y = br.ReadInt16();
                        vertices[t].Z = br.ReadInt16();
                        vertices[t].W = br.ReadInt16();
                    }
                }

            }
            /*End Construct Layers*/
        }

        private void LoadRoom(int index)
        {
            DateTime date1 = DateTime.Now;

            loaded_room = index;
            roomComboBox.SelectedIndex = loaded_room;
            Array.Clear(vramdata, 0, vramdata.Length);

            using (ZipArchive archive = new ZipArchive(new FileStream(isopath, FileMode.Open, FileAccess.Read, FileShare.Read), ZipArchiveMode.Read))
            {
                /*
                 * Construct VRAM
                 */
                loaded_room_name = roomNames[index];
                saveChip2Dialog.FileName = loaded_room_name + ".chip2";

                MemoryStream ctd_mem_stream = new MemoryStream();
                archive.GetEntry("map/mapbin/" + roomNames[index] + ".ctd").Open().CopyTo(ctd_mem_stream);
                ctd_mem_stream.Seek(0, SeekOrigin.Begin);
                ReadCTD(ctd_mem_stream);
                
                MemoryStream bin_mem_stream = new MemoryStream();
                archive.GetEntry("map/mapbin/" + roomNames[index] + ".bin").Open().CopyTo(bin_mem_stream);
                bin_mem_stream.Seek(0, SeekOrigin.Begin);
                ReadBIN(bin_mem_stream);
            }

            using (ZipArchive archive = new ZipArchive(new FileStream(Path.GetDirectoryName(isopath) + "/hd.dat", FileMode.Open, FileAccess.Read, FileShare.Read), ZipArchiveMode.Read))
            {
                MemoryStream hd_mem_stream = new MemoryStream();
                archive.GetEntry("map/mapbin/" + roomNames[index] + ".chip2").Open().CopyTo(hd_mem_stream);
                hd_mem_stream.Seek(0, SeekOrigin.Begin);
                hd_data = new short[hd_mem_stream.Length / 2];
                BinaryReader b = new BinaryReader(hd_mem_stream);
                for (int i = 0; i < hd_data.Length; i++) {
                    hd_data[i] = b.ReadInt16();
                }
            }
                
            /*
            using (FileStream fs = new FileStream(@"D:\vram.bin", FileMode.OpenOrCreate))
            {
                fs.Write(vramdata, 0, 2048 * 512);
            }*/
            flowLayoutPanel1.Controls.Clear();
            pictureBox1.Image = new Bitmap(4,4);
            if (layers != null)
            {
                cbgroup = new CheckBox[layers.Length];
                for (int i = 0; i < cbgroup.Length; i++)
                {
                    cbgroup[i] = new CheckBox();
                    cbgroup[i].Text = i.ToString();
                    cbgroup[i].Width = 40;
                    cbgroup[i].Checked = true;
                    cbgroup[i].CheckedChanged += SwitchDisplay;
                    flowLayoutPanel1.Controls.Add(cbgroup[i]);
                }
                Text = "Room " + roomNames[index] + " loaded in " + ((TimeSpan)(DateTime.Now - date1)).TotalSeconds + "s";
            }
            else
            {
                Text = "Room " + roomNames[index] + " is empty.";
            }
        }

        void SwitchDisplay(object sender, EventArgs e)
        {
            DrawLayers();
        }

        private void WriteCfg()
        {
            using (StreamWriter writer = new StreamWriter(new FileStream("view.cfg", FileMode.Create)))
            {
                if (lastdir != null) writer.WriteLine(lastdir);
                else writer.WriteLine("");
            }
        }

        private void LoadCfg()
        {
            using (StreamReader reader = new StreamReader(new FileStream("view.cfg", FileMode.OpenOrCreate)))
            {
                lastdir = reader.ReadLine();
            }
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            LoadCfg();
            openFileDialog1.InitialDirectory = lastdir;
            toolsPanel.Enabled = false;
        }

        private void Form1_Resize(object sender, EventArgs e)
        {
            flowLayoutPanel1.MaximumSize = new Size(Width-50, 0);
            flowLayoutPanel1.Width = Width - 50;
        }

        private void SaveChip2(string path) {
            using (BinaryWriter b = new BinaryWriter(new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite))) {
                for (int i = 0; i < hd_data.Length; i++) {
                    b.Write(hd_data[i]);
                }
            }
        }

        private void OnSaveChip2Click(object sender, EventArgs e) {
            saveChip2Dialog.Filter = "(*.chip2)|*.chip2";
            saveChip2Dialog.Title = "Save " + loaded_room_name + ".chip2";
            if (saveChip2Dialog.ShowDialog() == DialogResult.OK)
            {
                SaveChip2(saveChip2Dialog.FileName);
            }
        }

        enum ExportedPNGFormat {
            Opaque,
            SemiTransparent,
            OpaqueAndSTBlack,
        }

        private void ConvertTriToPSX(Bitmap src_img, short[] pixels) {
            // Legacy format for nobody
            BitmapData src_data = src_img.LockBits(new Rectangle(0, 0, src_img.Width, src_img.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            Debug.Assert(pixels.Length == src_img.Width * src_img.Height);
            unsafe {
                uint* src = (uint*)src_data.Scan0.ToPointer();
                for (int i = pixels.Length - 1; i >= 0; i--) {
                    uint rgb = src[i] & 0xffffff;
                    uint alpha = (src[i] >> 24) & 0xff;
                    uint r = (((rgb >> 16) & 0xff) * 0x1f + 0xff/2) / 0xff;
                    uint g = (((rgb >> 8) & 0xff) * 0x1f + 0xff/2) / 0xff;
                    uint b = (((rgb >> 0) & 0xff) * 0x1f + 0xff/2) / 0xff;
                    uint a;
                    if (alpha == 0) {
                        r = 0;
                        g = 0;
                        b = 0;
                        a = 0;
                    } else if (alpha == 255) {
                        a = 0;
                        if (r == 0 && g == 0 && b == 0) {
                            b = 1;
                        }
                    } else {
                        a = 1;
                    }
                    pixels[i] = (short)((r & 0x1f) | ((g & 0x1f) << 5) | ((b & 0x1f) << 10) | (a << 15));
                }
            }
            src_img.UnlockBits(src_data);
        }

        private void ConvertToPSX(Bitmap src_img, short[] pixels, ExportedPNGFormat format) {
            BitmapData src_data = src_img.LockBits(new Rectangle(0, 0, src_img.Width, src_img.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            Debug.Assert(pixels.Length == src_img.Width * src_img.Height);
            unsafe {
                uint* src = (uint*)src_data.Scan0.ToPointer();
                for (int i = pixels.Length - 1; i >= 0; i--) {
                    uint rgb = src[i] & 0xffffff;
                    uint alpha = (src[i] >> 24) & 0xff;
                    if (alpha < 127) { // Arbitrary threshold
                        // Do nothing
                    } else {
                        // Replace with pixel
                        uint r = (((rgb >> 16) & 0xff) * 0x1f + 0xff/2) / 0xff;
                        uint g = (((rgb >> 8) & 0xff) * 0x1f + 0xff/2) / 0xff;
                        uint b = (((rgb >> 0) & 0xff) * 0x1f + 0xff/2) / 0xff;
                        uint a = format == ExportedPNGFormat.SemiTransparent ? 1u : 0u;
                        short c = (short)((r & 0x1f) | ((g & 0x1f) << 5) | ((b & 0x1f) << 10) | (a << 15));
                        if (c == 0) { // Opaque black, will be interpreted as transparent unless adjusted
                            if (format == ExportedPNGFormat.OpaqueAndSTBlack) {
                                c = 1; // Oh my god shut up, compiler
                                c <<= 15; // Use semi-transparent black
                            } else {
                                c = 1 << 10; // Use darkest blue
                            }
                        } 
                        pixels[i] = c;
                    }
                }
            }
            src_img.UnlockBits(src_data);
        }

        private Bitmap ReadHDLayerPNG(string path, Layer l) {
            Bitmap img = new Bitmap(path, false);
            if (img.Width == l.Bounds.Width * 4 && img.Height == l.Bounds.Height * 4) {
                return img;
            } else if (img.Width == roomBounds.Width * 4 && img.Height == roomBounds.Height * 4) {
                img = img.Clone(new Rectangle(
                    (l.Bounds.X - roomBounds.X) * 4,
                    (l.Bounds.Y - roomBounds.Y) * 4,
                    l.Bounds.Width * 4, l.Bounds.Height * 4
                ), img.PixelFormat);
                return img;
            } else {
                complain(path + " must have dimensions " +
                    l.Bounds.Width * 4 + " x " + l.Bounds.Height * 4 + " or " +
                    roomBounds.Width * 4 + " x " + roomBounds.Height * 4
                );
            }
            return null;
        }

        bool is_batch_processing = false;
        int num_complaints = 0;
        void complain(string im_mad) {
            if (is_batch_processing) {
                num_complaints += 1;
                Console.Error.WriteLine(im_mad);
            } else {
                MessageBox.Show(im_mad);
            }
        }
        void lodge_complaints() {
            if (num_complaints > 0) {
                MessageBox.Show("Batch processing logged " + num_complaints + " errors to stderr.");
                num_complaints = 0;
            }
        }

        class Tile : IComparable<Tile> {
            public short[] pixels = new short[64 * 64];

            public int CompareTo(Tile other)
            {
                int r = pixels.Length.CompareTo(other.pixels.Length);
                for (int i = 0; r == 0 && i < pixels.Length; i++) {
                    r = pixels[i].CompareTo(other.pixels[i]);
                }
                return r;
            }
        }
        class TileSet {
            const int NUM_TILE_COLUMNS = 128;
            const int NUM_TILE_ROWS = 16;
            const int NUM_TILE_PALETTES = 32;

            Dictionary<int, Tile> tiles = new Dictionary<int, Tile>();

            public TileSet() {
                // Empty tileset
            }

            public TileSet(short[] chip2) {
                for (int row = 0; row < NUM_TILE_ROWS * NUM_TILE_PALETTES; row++) {
                    for (int col = 0; col < NUM_TILE_COLUMNS; col++) {
                        int id = col + row * NUM_TILE_COLUMNS;
                        int hd_tile_index = chip2[id];
                        if (hd_tile_index != 0) {
                            tiles[id] = new Tile();
                            Array.Copy(chip2, hd_tile_index * 64 * 64, tiles[id].pixels, 0, 64 * 64);
                        }
                    }
                }
            }

            public Tile get_tile(int row, int col, int palette) {
                int id = (row + palette * NUM_TILE_ROWS) * NUM_TILE_COLUMNS + col;
                if (!tiles.ContainsKey(id)) {
                    tiles[id] = new Tile();
                }
                return tiles[id];
            }

            public short[] convert_to_chip2() {
                SortedDictionary<Tile, short> uniqueTiles = new SortedDictionary<Tile, short>(); // Used to deduplicate tiles

                short[] chip2 = new short[NUM_TILE_COLUMNS * NUM_TILE_ROWS * NUM_TILE_PALETTES + tiles.Count * 64 * 64];
                int tilesWritten = 16; // Starts at 16. NUM_TILE_COLUMNS * NUM_TILE_ROWS * NUM_TILE_PALETTES == 16 * 64 * 64
                for (int row = 0; row < NUM_TILE_ROWS * NUM_TILE_PALETTES; row++) {
                    for (int col = 0; col < NUM_TILE_COLUMNS; col++) {
                        int id = col + row * NUM_TILE_COLUMNS;
                        if (tiles.ContainsKey(id)) {
                            Tile t = tiles[id];
                            if (uniqueTiles.ContainsKey(t)) {
                                chip2[id] = uniqueTiles[t];
                            } else {
                                uniqueTiles[t] = (short)tilesWritten;
                                Array.Copy(t.pixels, 0, chip2, tilesWritten * 64 * 64, 64 * 64);
                                chip2[id] = (short)tilesWritten;
                                tilesWritten += 1;
                            }
                        } else {
                            chip2[id] = 0;
                        }
                    }
                }
                // Console.WriteLine("Deduped tiles: " + (tiles.Count - uniqueTiles.Count));
                Array.Resize(ref chip2, tilesWritten * 64 * 64);
                return chip2;
            }
        }

        private bool ImportHDLayer(string from_dir, Layer l, TileSet ts) {
            int w = l.Bounds.Width * 4;
            int h = l.Bounds.Height * 4;
            short[] layer_image = new short[w * h];

            bool file_found = false;
            try { 
                string filename = from_dir + "/" + loaded_room_name + "-layer" + l.Index + "-st.png";
                Bitmap maybe_st = ReadHDLayerPNG(filename, l);
                if (maybe_st != null) {
                    file_found = true;
                    ConvertToPSX(maybe_st, layer_image, ExportedPNGFormat.SemiTransparent);
                }
            } catch { }
            try { 
                string filename = from_dir + "/" + loaded_room_name + "-layer" + l.Index + "-op.png";
                Bitmap maybe_op = ReadHDLayerPNG(filename, l);
                if (maybe_op != null) {
                    file_found = true;
                    ConvertToPSX(maybe_op, layer_image, ExportedPNGFormat.Opaque);
                }
            } catch { }
            try { 
                string filename = from_dir + "/" + loaded_room_name + "-layer" + l.Index + "-opb.png";
                Bitmap maybe_opb = ReadHDLayerPNG(filename, l);
                if (maybe_opb != null) {
                    file_found = true;
                    ConvertToPSX(maybe_opb, layer_image, ExportedPNGFormat.OpaqueAndSTBlack);
                }
            } catch { }
            try { 
                string filename = from_dir + "/" + loaded_room_name + "-layer" + l.Index + ".tri.png";
                Bitmap maybe_tri = ReadHDLayerPNG(filename, l);
                if (maybe_tri != null) {
                    file_found = true;
                    ConvertTriToPSX(maybe_tri, layer_image);
                }
            } catch { }
            if (!file_found) {
                complain("No valid imports for layer " + l.Index + " of room " + loaded_room_name);
                return false;
            }

            foreach (var cmd in l.CmdList)
            {
                Debug.Assert(cmd.tileX % 8 == 0);
                Debug.Assert(cmd.tileY % 16 == 0);
                Debug.Assert(cmd.paletteIndex % 8 == 0);
                reverseBlitTileDestination = ts.get_tile(cmd.tileY / 16, cmd.tileX / 8, cmd.paletteIndex / 8).pixels;
                DrawTile(cmd, l.Bounds, layer_image, TileDrawMode.ReverseBlit);
            }

            return true;
        }

        private bool ImportHD(string from_dir) {
            bool any_imported = false;
            TileSet ts = new TileSet(hd_data);
            for (int i = layers.Length - 1; i >= 0; i--) {
                any_imported |= ImportHDLayer(from_dir, layers[i], ts);
            }
            hd_data = ts.convert_to_chip2();
            if (!any_imported) {
                complain("No valid imports for room " + loaded_room_name);
            }
            return any_imported;
        }

        private void OnBatchConvertClick(object sender, EventArgs e) {
            if (folderImportDialog.ShowDialog() == DialogResult.OK &&
                saveBatchChip2Dialog.ShowDialog() == DialogResult.OK)
            {
                if (!show_hd) {
                    hdCheckBox.Checked = true;
                    show_hd = true;
                }
                is_batch_processing = true;
                for (int r = 0; r < total_rooms; r++) {
                    LoadRoom(r);
                    if (layers == null) continue;
                    if (ImportHD(folderImportDialog.SelectedPath)) {
                        SaveChip2(saveBatchChip2Dialog.SelectedPath + "/" + loaded_room_name + ".chip2");
                    }
                }
                is_batch_processing = false;
                lodge_complaints();
            }
        }

        private void OnImportClick(object sender, EventArgs e)
        {
            if (folderImportDialog.ShowDialog() == DialogResult.OK)
            {
                if (!show_hd) {
                    hdCheckBox.Checked = true;
                    show_hd = true;
                    // DrawLayers();
                }
                ImportHD(folderImportDialog.SelectedPath);
                DrawLayers();
            }
        }

        private void OnExportClick(object sender, EventArgs e)
        {
            if (folderExportDialog.ShowDialog() == DialogResult.OK)
            {
                string path = folderExportDialog.SelectedPath;
                for (int i = 0; i < layers.Length; i++) {
                    Layer l = layers[i];
                    SaveRawLayer(DrawRawLayer(l), path + "/" + loaded_room_name + "-layer" + l.Index);
                }
            }
        }

        private void OnExportAllClick(object sender, EventArgs e)
        {
            if (folderExportDialog.ShowDialog() == DialogResult.OK)
            {
                string path = folderExportDialog.SelectedPath;
                for (int r = 0; r < total_rooms; r++) {
                    LoadRoom(r);
                    for (int i = 0; i < layers.Length; i++) {
                        Layer l = layers[i];
                        SaveRawLayer(DrawRawLayer(l), path + "/" + loaded_room_name + "-layer" + l.Index);
                    }
                }
            }
        }

        private void SaveRawLayer(Bitmap src_bitmap, string path) {
            int w = src_bitmap.Width;
            int h = src_bitmap.Height;
            unsafe
            {
                BitmapData src_data = src_bitmap.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
                uint* src = (uint*)src_data.Scan0.ToPointer();

                Bitmap dst_img = new Bitmap(w, h, PixelFormat.Format32bppArgb);
                // Semi-transparent
                bool semi_transparent_pixels_exist = false;
                bool semi_transparent_all_black = true;
                bool semi_transparent_written = false;
                {
                    BitmapData semi_trans_image_data = dst_img.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
                    uint* dst = (uint*)semi_trans_image_data.Scan0.ToPointer();
                    for (int i = w * h - 1; i >= 0; i--) {
                        uint rgb = src[i] & 0xffffff;
                        uint a = (src[i] >> 24) & 0xff;
                        uint triA;
                        if (rgb == 0 && a == 0) {
                            // Transparent
                            triA = 0;
                        } else if (rgb != 0 && a == 0) {
                            triA = 0;
                        } else {
                            semi_transparent_pixels_exist = true;
                            semi_transparent_all_black &= rgb == 0;
                            triA = 255;
                        }
                        dst[i] = (triA << 24) | rgb;
                    }
                    dst_img.UnlockBits(semi_trans_image_data);
                    if (semi_transparent_pixels_exist && !semi_transparent_all_black) {
                        semi_transparent_written = true;
                        dst_img.Save(path + "-st.png", ImageFormat.Png);
                    }
                }
                
                // Opaque
                bool opaque_pixels_exist = false;
                {
                    BitmapData opaque_image_data = dst_img.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
                    uint* dst = (uint*)opaque_image_data.Scan0.ToPointer();
                    for (int i = w * h - 1; i >= 0; i--) {
                        uint rgb = src[i] & 0xffffff;
                        uint a = (src[i] >> 24) & 0xff;
                        uint triA;
                        if (rgb == 0 && a == 0) {
                            // Transparent
                            triA = 0;
                        } else if (rgb != 0 && a == 0) {
                            opaque_pixels_exist = true;
                            triA = 255;
                        } else {
                            if (semi_transparent_all_black) {
                                triA = 255;
                            } else {
                                triA = 0;
                            }
                        }
                        dst[i] = (triA << 24) | rgb;
                    }
                    dst_img.UnlockBits(opaque_image_data);
                    if (semi_transparent_pixels_exist && semi_transparent_all_black) {
                        dst_img.Save(path + "-opb.png", ImageFormat.Png);
                    } else if (opaque_pixels_exist || !semi_transparent_written) {
                        dst_img.Save(path + "-op.png", ImageFormat.Png);
                    }
                }
            }
        }

        private Bitmap DrawRawLayer(Layer layer)
        {
            Rectangle bounds = cropExportedLayersCheckBox.Checked ? layer.Bounds : roomBounds;

            int w = bounds.Width * (show_hd ? 4 : 1);
            int h = bounds.Height * (show_hd ? 4 : 1);
            short[] buffer = new short[w * h];

            foreach (var cmd in layer.CmdList)
            {
                DrawTile(cmd, bounds, buffer, TileDrawMode.Blit);
            }

            Bitmap rgba = new Bitmap(w, h, PixelFormat.Format32bppArgb);
            BitmapData rgba_data = rgba.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            unsafe
            {
                uint* rgba_buffer = (uint*)rgba_data.Scan0.ToPointer();
                for (int i = 0; i < buffer.Length; i++) {
                    uint sc = (uint)buffer[i];
                    uint sa = (sc >> 15) & 1;
                    uint sb = (sc >> 10) & 0x1F;
                    uint sg = (sc >> 5) & 0x1F;
                    uint sr = sc & 0x1F;
                    
                    uint r8 = (sr * 0xff + 0x1f/2) / 0x1f;
                    uint g8 = (sg * 0xff + 0x1f/2) / 0x1f;
                    uint b8 = (sb * 0xff + 0x1f/2) / 0x1f;
                    uint a8 = sa * 0xff;
                    rgba_buffer[i] = (a8 << 24) | (r8 << 16) | (g8 << 8) | b8;
                }
            }
            rgba.UnlockBits(rgba_data);
            return rgba;
        }

        enum TileDrawMode {
            Normal,
            Blit,
            ReverseBlit
        }

        short[] reverseBlitTileDestination;

        private void DrawTile(TileCmd cmd, Rectangle destBounds, short[] destBuffer, TileDrawMode mode)
        {
            int bx = destBounds.X;
            int by = destBounds.Y;
            int width = destBounds.Width;

            int tile_size = show_hd ? 64 : 16;
            int hd_tile_index = -1;
            if (show_hd) {
                Debug.Assert(cmd.tileY % 16 == 0);
                Debug.Assert(cmd.tileX % 8 == 0);
                Debug.Assert(cmd.paletteIndex % 8 == 0);
                hd_tile_index = hd_data[cmd.tileX / 8 + (cmd.tileY / 16 + cmd.paletteIndex * 2) * 128];
                if (hd_tile_index == 0) return;
            }

            for (int w = 0; w < tile_size; w++)
            {
                for (int h = 0; h < tile_size; h++)
                {
                    int dp;
                    int sc;
                    if (!show_hd) {
                        int dx = cmd.x - bx + w;
                        int dy = cmd.y - by + h;
                        dp = dy * width + dx;
                        int sp = vramdata[(cmd.v + h) * 2048 + cmd.u + w];
                        sc = vramdata[cmd.p + sp * 2] | (vramdata[cmd.p + sp * 2 + 1] << 8);
                    } else {
                        int dx = (cmd.x - bx) * 4 + w;
                        int dy = (cmd.y - by) * 4 + h;
                        dp = dy * width * 4 + dx;
                        int hd_offset = (hd_tile_index * 64 + h) * 64 + w;
                        sc = hd_data[hd_offset];
                        if (mode == TileDrawMode.ReverseBlit) {
                            reverseBlitTileDestination[h * tile_size + w] = destBuffer[dp];
                            // hd_data[hd_offset] = destBuffer[dp];
                        }
                    }

                    if (mode == TileDrawMode.Blit) {
                        destBuffer[dp] = (short)sc;
                    } else if (mode == TileDrawMode.Normal) {
                        bool alpha = cmd.mode != 0x20;
                        if (alpha && (sc & 0x8000)!=0) 
                        {

                            int sb = (sc >> 10) & 0x1F;
                            int sg = (sc >> 5) & 0x1F;
                            int sr = sc & 0x1F;

                            int dc = destBuffer[dp]&0xffff;
                            int dr = (dc >> 10) & 0x1F;
                            int dg = (dc >> 5) & 0x1F;
                            int db = dc & 0x1F;

                            int r, g, b;
                            /*
                            // RGB multipler
                            sr *= lr; sr /= 0x80; if (sr > 0x1f) sr = 0x1f;
                            sg *= lg; sg /= 0x80; if (sg > 0x1f) sg = 0x1f;
                            sb *= lb; sb /= 0x80; if (sb > 0x1f) sb = 0x1f;
                            */
                            /*
                            00  0.5B+0.5F 
                            01  1.0B+0.5F  
                            10  1.0B-1.0F
                            11  1.0B+0.25F*/
                            switch (cmd.mode)
                            {/*
                                    r = (sr + dr) >> 1;
                                    g = (sg + dg) >> 1;
                                    b = (sb + db) >> 1;
                                    break;*/
                                case 0x28:
                                case 0xa8:
                                    r = sr + dr;
                                    g = sg + dg;
                                    b = sb + db;
                                    break;
                                case 0xb0:

                                    r = dr - sr; if (r < 0) r = 0;
                                    g = dg - sg; if (g < 0) g = 0;
                                    b = db - sb; if (b < 0) b = 0;
                                    break;
                                default:
                                    r = (sr >> 2) + dr;
                                    g = (sg >> 2) + dg;
                                    b = (sb >> 2) + db;
                                    break;
                            }
                            if (r > 0x1F) r = 0x1F;
                            if (g > 0x1F) g = 0x1F;
                            if (b > 0x1F) b = 0x1F;
                            destBuffer[dp] = (short)((r << 10) | (g << 5) | b);
                        }
                        else
                        {
                            if ((sc & 0x7FFF) != 0)
                                destBuffer[dp] = (short)(((sc << 10) & 0x7C00) | (sc & 0x3E0) | ((sc >> 10) & 0x1F));
                        }
                    }
                }
            }
        }

        private void DrawLineZoom(Graphics g, Pen pen, int x1, int y1, int x2, int y2, float zoom)
        {
            int dx = 320;
            int dy = 240;
            g.DrawLine(pen, x1 * zoom + dx, -y1 * zoom + dy, x2 * zoom + dx, -y2 * zoom + dy);
        }

        private void DrawTriangleZoom(Graphics g, Brush brush, Point[] points, float zoom)
        {
            int dx = 320;
            int dy = 240;
            for (int i = 0; i < points.Length; i++)
            {
                points[i].X = (int)(points[i].X * zoom + dx);
                points[i].Y = (int)(-points[i].Y * zoom + dy);
            }
            g.FillPolygon(brush, points);
        }

        private void DrawLayers()
        {
            if(layers==null) return;

            int imWidth = roomBounds.Width * (show_hd ? 4 : 1);
            int imHeight = roomBounds.Height * (show_hd ? 4 : 1);
            if (layerbuffer == null || layerbuffer.Length != imWidth * imHeight) {
                layerbuffer = new short[imWidth * imHeight];
            }
            if (pictureBox1.Image.Width != imWidth || pictureBox1.Image.Height != imHeight) {
                pictureBox1.Image = new Bitmap(imWidth, imHeight);
            }
            updatePictureBoxSize();

            Bitmap dest = (Bitmap)(pictureBox1.Image);
            using (Graphics g = Graphics.FromImage(dest))
            {
                g.DrawString("Painting...", new Font(FontFamily.GenericSerif, 12.0f, FontStyle.Bold), Brushes.Red, new PointF(10, 10));
                g.Flush();
            }
            pictureBox1.Refresh();
            Enabled = false;
            Array.Sort(layers, CompareByZ);
            Array.Clear(layerbuffer, 0, layerbuffer.Length);
            
            for (int i = 0; i < layers.Length; i++)
            {
                if (cbgroup[layers[i].Index].Checked)
                {
                    TileCmd[] cmdlist = layers[i].CmdList;
                    for (int l = 0; l < cmdlist.Length; l++)
                    {
                        DrawTile(cmdlist[l], roomBounds, layerbuffer, TileDrawMode.Normal);
                    }
                }
            }
            
            
            //BitmapData bdata = dest.LockBits(new Rectangle(0, 0, dest.Width, dest.Height), ImageLockMode.WriteOnly, PixelFormat.Format16bppRgb555);
            // Mono doesn't support PixelFormat.Format16bppRgb555, so do the conversion ourself
            BitmapData bdata = dest.LockBits(new Rectangle(0, 0, dest.Width, dest.Height), ImageLockMode.WriteOnly, PixelFormat.Format32bppRgb);
            unsafe
            {
                uint* ptr = (uint*)bdata.Scan0.ToPointer();
                for (int i = dest.Width * dest.Height - 1; i >= 0; i--) {
                    int r5 = (layerbuffer[i] >> 10) & 0x1F;
                    int g5 = (layerbuffer[i] >> 5)  & 0x1F;
                    int b5 = (layerbuffer[i] >> 0)  & 0x1F;
                    int r8 = (r5 * 0xff + 0x1f/2) / 0x1f;
                    int g8 = (g5 * 0xff + 0x1f/2) / 0x1f;
                    int b8 = (b5 * 0xff + 0x1f/2) / 0x1f;
                    ptr[i] = 0xff000000 | (uint)((r8 << 16) | (g8 << 8) | b8);
                }
            }
            dest.UnlockBits(bdata);
            
            
            /*
            using (Graphics g = Graphics.FromImage(dest))
            {
                for (int i = 0; i < triangles.Length; i++)
                {
                    DrawTriangleZoom(g, new SolidBrush(Color.FromArgb(96, Color.Red)), new Point[]{
                        new Point(vertices[triangles[i].Vertices[0]].X - layers[0].BeginX, vertices[triangles[i].Vertices[0]].Z - layers[0].BeginY),
                        new Point(vertices[triangles[i].Vertices[1]].X - layers[0].BeginX, vertices[triangles[i].Vertices[1]].Z - layers[0].BeginY),
                        new Point(vertices[triangles[i].Vertices[2]].X - layers[0].BeginX, vertices[triangles[i].Vertices[2]].Z - layers[0].BeginY),
                    }, 0.02f);
                    
                    //DrawLineZoom(g, Pens.White, vertices[triangles[i].Vertices[0]].X - layers[0].BeginX, vertices[triangles[i].Vertices[0]].Z - layers[0].BeginY, vertices[triangles[i].Vertices[1]].X - layers[0].BeginX, vertices[triangles[i].Vertices[1]].Z - layers[0].BeginY, 0.02f);
                    //DrawLineZoom(g, Pens.White, vertices[triangles[i].Vertices[1]].X - layers[0].BeginX, vertices[triangles[i].Vertices[1]].Z - layers[0].BeginY, vertices[triangles[i].Vertices[2]].X - layers[0].BeginX, vertices[triangles[i].Vertices[2]].Z - layers[0].BeginY, 0.02f);
                    //DrawLineZoom(g, Pens.White, vertices[triangles[i].Vertices[2]].X - layers[0].BeginX, vertices[triangles[i].Vertices[2]].Z - layers[0].BeginY, vertices[triangles[i].Vertices[0]].X - layers[0].BeginX, vertices[triangles[i].Vertices[0]].Z - layers[0].BeginY, 0.02f);
                }
            }*/
            Enabled = true;
            pictureBox1.Refresh();
        }

        private void openToolStripMenuItem_Click(object sender, EventArgs e)
        {
            openFileDialog1.InitialDirectory = lastdir;
            openFileDialog1.Title = "Select cdrom.dat.";
            if (openFileDialog1.ShowDialog() == DialogResult.OK)
            {
                //try
                {
                    isopath = openFileDialog1.FileName;

                    roomNames.Clear();
                    using (ZipArchive archive = new ZipArchive(new FileStream(isopath, FileMode.Open, FileAccess.Read, FileShare.Read), ZipArchiveMode.Read))
                    {
                        foreach (ZipArchiveEntry entry in archive.Entries) {
                            if (entry.FullName.StartsWith("map/mapbin/") && entry.Name.EndsWith(".ctd")) {
                                roomNames.Add(Path.GetFileNameWithoutExtension(entry.Name));
                            }
                        }
                        total_rooms = roomNames.Count;
                    }

                    roomComboBox.Items.Clear();
                    roomComboBox.Items.AddRange(roomNames.ToArray());
                    toolsPanel.Enabled = true;

                    LoadRoom(0);

                    lastdir = new FileInfo(openFileDialog1.FileName).Directory.FullName;
                    WriteCfg();
                    DrawLayers();
                }
                /*catch
                {
                    MessageBox.Show("There's something wrong...");
                }*/
            }
        }

        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Application.Exit();
        }

        private void toolStripMenuItem2_Click(object sender, EventArgs e)
        {
            if (pictureBox1.Image != null)
            {
                saveFileDialog1.Filter = "(*.png)|*.png|(*.bmp)|*.bmp";
                if (saveFileDialog1.ShowDialog() == DialogResult.OK)
                {
                    if(saveFileDialog1.FilterIndex==1)
                        pictureBox1.Image.Save(saveFileDialog1.FileName, ImageFormat.Png);
                    else
                        pictureBox1.Image.Save(saveFileDialog1.FileName, ImageFormat.Bmp);
                    WriteCfg();
                }
            }
            else
            {
                MessageBox.Show("Nothing to save.");
            }
        }

        private void roomComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (loaded_room != roomComboBox.SelectedIndex) {
                LoadRoom(roomComboBox.SelectedIndex);
                DrawLayers();
            }
        }

        private void hdCheckBox_ValueChanged(object sender, EventArgs e)
        {
            show_hd = hdCheckBox.Checked;
            // LoadRoom((int)numericUpDown1.Value);
            DrawLayers();
        }

        private void updatePictureBoxSize() {
            if (pictureBox1.Image == null) return;
            if (zoomPreviewCheckBox.Checked) {
                pictureBox1.ClientSize = new Size(imageScrollPanel.Width, imageScrollPanel.Height);
            } else {
                pictureBox1.ClientSize = pictureBox1.Image.Size;
            }
        }

        private void zoomPreviewCheckBox_ValueChanged(object sender, EventArgs e)
        {
            updatePictureBoxSize();
        }

        private void OnResize(object sender, EventArgs e) {
            updatePictureBoxSize();
        }

    }

}