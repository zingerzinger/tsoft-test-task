using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

using Grpc.Core;
using Proto;

namespace Client
{
    public static class COMMON
    {
        public const int NET_PORT = 7777;

        public static Color[] COLOR_MAP = new Color[] {
            Colors.Red    , Colors.Green        , Colors.Blue           ,
            Colors.Magenta, Colors.Yellow       , Colors.Orange         ,
            Colors.Orchid , Colors.PaleGoldenrod, Colors.PaleGreen      ,
            Colors.Lime   , Colors.LimeGreen    , Colors.MediumSlateBlue
        };
    }

    internal class ViewModel
    {
        public static ObservableCollection<VBody> VBodies { get; set; } = new ObservableCollection<VBody>();
    }

    internal class VBody : INotifyPropertyChanged
    {
        private float x;
        private float y;

        public float X
        {
            get { return x; }
            set { x = value; OnPropertyChanged("X"); }
        }

        public float Y
        {
            get { return y; }
            set { y = value; OnPropertyChanged("Y"); }
        }

        public Color Color { get; init; }

        public event PropertyChangedEventHandler PropertyChanged;
        public void OnPropertyChanged([CallerMemberName] string prop = "") {
            if (PropertyChanged != null) { PropertyChanged(this, new PropertyChangedEventArgs(prop)); }
        }
    }

    public partial class MainWindow : Window
    {
        Dictionary<int, int> bodiesMap = new Dictionary<int, int>(); // Id -> VBodies index
        int colorIdx = 0;

        public MainWindow() {
            InitializeComponent();

            var channel = new Channel("localhost", COMMON.NET_PORT, ChannelCredentials.Insecure);
            var client = new Svc.SvcClient(channel);
            BodyDataStream(client);
        }

        async Task BodyDataStream(Svc.SvcClient client) {

            using var reply = client.BodyDataService(new Request());

            while (true) {
                await reply.ResponseStream.MoveNext();
                var body = reply.ResponseStream.Current;

                if (!bodiesMap.ContainsKey(body.Id)) {
                    bodiesMap.Add(body.Id, ViewModel.VBodies.Count);
                    ViewModel.VBodies.Add(new VBody { X = body.X, Y = body.Y, Color = COMMON.COLOR_MAP[colorIdx] });
                    colorIdx = (colorIdx + 1) % COMMON.COLOR_MAP.Length;
                } else {
                    VBody vbody = ViewModel.VBodies[body.Id];
                    vbody.X = body.X;
                    vbody.Y = body.Y;
                }
            }
        }
    }
}
