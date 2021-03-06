﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace MiniGIS
{
    public partial class Map : UserControl
    {
        internal List<MapObject> SelectedObjects = new List<MapObject>();
        protected List<VectorLayer> layers = new List<VectorLayer>();
        public List<VectorLayer> Layers => layers;
        public string Name;
        private Vertex center = new Vertex(0, 0);
        private double mapScale = 1;
        private const int snap = 2;
        public Color SelectionColor { get; set; } = Color.Blue;
        public Vertex Center
        {
            get { return center; }
            set
            {
                center = value;
                Invalidate();
            }
        }
        public double MapScale
        {
            get { return mapScale; }
            set { if(mapScale<1024|| value<mapScale)

                    mapScale = value;
                Invalidate();
            }
        }
        private bool IsMouseDown;
        private System.Drawing.Point mouseDownPosition;
        private MapToolType activeTool;
        public Bounds Bounds { get { return UpdateBounds(); } }

        
        public Bounds UpdateBounds()
        {
            Bounds b = new Bounds();
            foreach (var layer in Layers)
            {
                if(layer.Visible)
                b = b.UnionBounds(layer.Bounds);
            }
            return b;
        }
        public MapToolType ActiveTool
        {
            get
            {
                return activeTool;
            }
            set
            {
                activeTool = value;
                switch (ActiveTool)
                {
                    case MapToolType.Select:
                        Cursor = Cursors.Arrow;
                        break;
                    case MapToolType.Pan:
                        Cursor = LoadCustomCursor(Path.GetFullPath(@"HandMoveGrab.cur"));
                        
                        break;
                    case MapToolType.ZoomIn:
                        Cursor = LoadCustomCursor(Path.GetFullPath(@"zoomin.cur"));
                        break;
                    case MapToolType.ZoomOut:
                        Cursor = LoadCustomCursor(Path.GetFullPath(@"zoomout.cur"));
                        break;
                }
            }
        }
        public static Cursor LoadCustomCursor(string path)
        {
            IntPtr hCurs = LoadCursorFromFile(path);
            if (hCurs == IntPtr.Zero) throw new Win32Exception();
            var curs = new Cursor(hCurs);
            // Note: force the cursor to own the handle so it gets released properly
            var fi = typeof(Cursor).GetField("ownHandle", BindingFlags.NonPublic | BindingFlags.Instance);
            fi.SetValue(curs, true);
            return curs;
        }

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern IntPtr LoadCursorFromFile(string path);

        public Map()
        {
            InitializeComponent();
            MouseWheel += Map_MouseWheel;
        }
        public void AddLayer(VectorLayer lay)
        {
            layers.Add(lay);
            lay.map = this;
        }
        public void RemoveLayer(int index)
        {
            VectorLayer lay = layers[index];
            foreach (var obj in lay.Objects) obj.Layers.Remove(lay);
            layers.RemoveAt(index);
        }
        public void RemoveLayer(VectorLayer item)
        {
            if (item.Objects.Count!=0) 
            foreach (var obj in item.Objects) obj.Layers.Remove(item);
            layers.Remove(item);
        }
        public void RemoveAllLayer()
        {
            layers.Clear();
            foreach (var lay in layers)
            {
                foreach (var obj in lay.Objects) obj.Layers.Remove(lay);
            }
        }
        
        public System.Drawing.Point MapToScreen(Vertex point)
        {
            System.Drawing.Point screenPoint = new System.Drawing.Point
            {
                X = (int) ((point.X - center.X) * mapScale + Width / 2 + 0.5),
                Y = (int) (-(point.Y - center.Y) * mapScale + Height / 2 + 0.5)
            };
            return screenPoint;
        }

        public Vertex ScreenToMap(System.Drawing.Point screenPoint)
        {
            Vertex point = new Vertex(0, 0)
            {
                X = ((screenPoint.X - Width / 2) / mapScale + center.X),
                Y = -(screenPoint.Y - Height / 2) / MapScale + Center.Y
            };
            return point;
        }

        private void Map_Paint(object sender, PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            foreach (var layer in layers)
            {
                layer.Draw(e);
            }
        }

        private void Map_Resize(object sender, EventArgs e)
        {
           Invalidate();
        }

        private void Map_MouseMove(object sender, MouseEventArgs e)
        {
            if (!IsMouseDown) return;
            
            switch (ActiveTool)
            {
                case MapToolType.Select:
                    break;
                case MapToolType.Pan:
                    var _dX = (e.X - mouseDownPosition.X)/MapScale;
                    var _dY = (e.Y - mouseDownPosition.Y) / MapScale;
                    Center.X -= _dX;
                    Center.Y += _dY;
                    Invalidate();
                    mouseDownPosition = e.Location;
                    break;
                case MapToolType.ZoomIn:
                    DrawRect(e);
                    break;
                case MapToolType.ZoomOut:
                    break;
            }
        }

        private void DrawRect(MouseEventArgs e)
        {
            var topLeft = new System.Drawing.Point
            {
                X = mouseDownPosition.X < e.X ? mouseDownPosition.X : e.X,
                Y = mouseDownPosition.Y < e.Y ? mouseDownPosition.Y : e.Y
            };
            var bottomRight = new System.Drawing.Point
            {
                X = mouseDownPosition.X > e.X ? mouseDownPosition.X : e.X,
                Y = mouseDownPosition.Y > e.Y ? mouseDownPosition.Y : e.Y
            };
            Graphics g;
            g = CreateGraphics();
            g.DrawRectangle(new Pen(Color.Black, 2), topLeft.X, topLeft.Y,
        bottomRight.X - topLeft.X, bottomRight.Y - topLeft.Y);
            Refresh();
        }

        private void Map_MouseDown(object sender, MouseEventArgs e)
        {
            IsMouseDown = true;
            mouseDownPosition = e.Location;
        }

        private void Map_MouseUp(object sender, MouseEventArgs e)
        {
            switch (ActiveTool)
            {
                case MapToolType.Select:
                    var dx = Math.Abs(mouseDownPosition.X - e.Location.X);
                    var dy = Math.Abs(mouseDownPosition.Y - e.Location.Y);
                    if (dx > snap || dy > snap) break;
                    var searchPoint = ScreenToMap(mouseDownPosition);
                    var d = snap / MapScale;
                    if (ModifierKeys != Keys.Control)
                    {
                        ClearSelection();
                        Invalidate();
                    }
                    var result = FindObject(searchPoint, d);
                    if (result == null) break;
                    result.Selected = true;
                    Invalidate();
                    break;
                case MapToolType.Pan:
                    break;
                case MapToolType.ZoomIn:
                    var w = Math.Abs(mouseDownPosition.X - e.Location.X);
                    var h = Math.Abs(mouseDownPosition.Y - e.Location.Y);
                    var screenPoint = new System.Drawing.Point
                    {
                        X = (mouseDownPosition.X + e.Location.X) / 2,
                        Y = (mouseDownPosition.Y + e.Location.Y) / 2
                    };
                    Center = ScreenToMap(screenPoint);
                    if (w==0 && h==0)
                    {
                        MapScale *= 1.5;
                    }
                    if (w == 0 && h != 0)
                    {
                        MapScale *= Height/h;
                    }
                    if (h == 0 && w != 0)
                    {
                        MapScale *= Width/w;
                    }
                    if (w!=0&&h!=0)
                    {
                        MapScale *= Math.Min(Width / w, Height / h);
                    }
                   
                    break;
                case MapToolType.ZoomOut:
                    MapScale /= 1.5;
                    break;
            }
            IsMouseDown = false;
        }

        private MapObject FindObject(Vertex searchPoint, double d)
        {
            MapObject result = null;
            for (var i = layers.Count - 1; i>=0; i--)
            {
                MapObject searchObj = layers[i].FindObject(searchPoint, d);
                if (searchObj != null)
                {
                    result = searchObj;
                    break;
                }
            }
            return result;
        }

        private void Map_MouseWheel(object sender, MouseEventArgs e)
        {
            if (e.Delta > 0) MapScale *= 2;
            else MapScale /= 2;
        }
        public void ZoomAll()
        {
            if (!Bounds.Valid) return;
            var h = (Bounds.YMax - Bounds.YMin) * MapScale;
            var w = (Bounds.XMax - Bounds.XMin) * MapScale;
            if (!(w<=snap||h<=snap))
                mapScale *= Math.Min(Width / w, Height / h);
            Center = new Vertex((Bounds.XMax + Bounds.XMin) / 2, (Bounds.YMax + Bounds.YMin) / 2);
            Invalidate();
        }

        public void ZoomLayers(List<VectorLayer> layers)
        {
            if (layers == null) return;
            Bounds b = new Bounds();
            foreach (var layer in layers)
            {
                if(layer.Visible)
                b = b.UnionBounds(layer.Bounds);
            }
            var h = (b.YMax - b.YMin) * MapScale;
            var w = (b.XMax - b.XMin) * MapScale;
            if (w != 0 && h != 0)
                mapScale *= Math.Min(Width / w, Height / h);
            Center = new Vertex((b.XMax + b.XMin) / 2, (b.YMax + b.YMin) / 2);
        }

        public void MoveLayerUp(VectorLayer layer)
        {
            if (!layers.Contains(layer)) return;
            int index = layers.IndexOf(layer);
            if (index == layers.Count - 1) return;
            layers.RemoveAt(index);
            layers.Insert(index + 1, layer);
        }
        public void MoveLayerDown(VectorLayer layer)
        {
            if (!layers.Contains(layer)) return;
            int index = layers.IndexOf(layer);
            if (index == 0) return;
            layers.RemoveAt(index);
            layers.Insert(index - 1, layer);
        }

        internal void ClearSelection()
        {
            foreach (var obj in SelectedObjects)
                obj.Selected = false;
            SelectedObjects.Clear();
        }

        internal void FindPolylines(MapObject polygon)
        {
            foreach(VectorLayer layer in Layers)
            {
                if(layer.Visible)
                layer.FindPolylines(polygon);
            }
            Invalidate();
        }
    }
}
