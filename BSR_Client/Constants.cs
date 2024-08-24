using System;
using System.Collections.Generic;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

#pragma warning disable IDE0079
#pragma warning disable IDE0044

namespace BSR_Client
{
    public enum EPacket
    {
        Connect,
        ConnectAck,
        StartGame,
        SetPlayer,
        DebugInfo,
        Shoot,
        SetBullets,
        HideBullets,
        UpdateLives,
        ReceiveItems,
        Disconnect,
        UseItem,
        BecomeAngry,
        ChangeMusic,
        Dead,
        ExtraBullet,
        ItemTrashed,
    }

    public enum EBullet
    {
        Blank,
        Live,
    }

    public enum EItem
    {
        Nothing,
        Handcuffs,
        Cigarettes,
        Saw,
        Magnifying,
        Beer,
        Inverter,
        Medicine,
        Phone,
        Adrenaline,
        Magazine,
        Gunpowder,
        //Bullet,
        Trashbin,
        Count,
    }

    public partial class MainWindow
    {
        private List<string> Players = new List<string>();
        private List<string> DeadPlayers = new List<string>();
        private int PlayersAlive = 0;
        private Client Sync = null;
        private string IP = "";
        private int PORT = 19123;
        private string MyName;
        private List<string> PlayerTurns = new List<string>();
        private Random RNG = new Random();
        private List<EBullet> Bullets = new List<EBullet>();
        private EBullet[] InitialBullets = new EBullet[8];
        private int InitialBulletCount = 0;
        private bool WasPlayingSounds = false;
        private bool NextShotSawed = false;
        private bool NextShotGunpowdered = false;
        private bool CanShootAgain = false;
        private bool PacketHandled = false;
        private bool UsedTrashBin = false;
        private string GameVersion = "";

        public string GetGameVersion() => GameVersion;

        private class ElementLib
        {
            public Rectangle[] Bullets;
            public ProgressBar[] HealthBars;
            public Button[] Items;
            public Button[] Players;

            public ElementLib(MainWindow w)
            {
                Bullets = new Rectangle[] { w.Bullet1, w.Bullet2, w.Bullet3, w.Bullet4, w.Bullet5, w.Bullet6, w.Bullet7, w.Bullet8 };
                HealthBars = new ProgressBar[] { w.Health1, w.Health2, w.Health3, w.Health4, w.Health5 };
                Items = new Button[] { w.Item1, w.Item2, w.Item3, w.Item4, w.Item5, w.Item6, w.Item7, w.Item8 };
                Players = new Button[] { w.Player1, w.Player2, w.Player3, w.Player4, w.Player5 };
            }
        }

        private ElementLib Elements = null;

        private class SoundLib
        {
            public MediaPlayer Title = new MediaPlayer() { Volume = 0.05 };
            public MediaPlayer Background1 = new MediaPlayer() { Volume = 0.05 };
            public MediaPlayer Background2 = new MediaPlayer() { Volume = 0.05 };
            public MediaPlayer End = new MediaPlayer() { Volume = 0.05 };
            public MediaPlayer Empty = new MediaPlayer() { Volume = 1.0 };
            public MediaPlayer Shot = new MediaPlayer() { Volume = 1.0 };
            public MediaPlayer GunpowderShot = new MediaPlayer() { Volume = 1.0 };
            public MediaPlayer Saw = new MediaPlayer() { Volume = 1.0 };
            public MediaPlayer Magnify = new MediaPlayer() { Volume = 1.0 };
            public MediaPlayer Beer = new MediaPlayer() { Volume = 1.0 };
            public MediaPlayer Cig = new MediaPlayer() { Volume = 1.0 };
            public MediaPlayer Handcuff = new MediaPlayer() { Volume = 1.0 };
            public MediaPlayer Inverter = new MediaPlayer() { Volume= 1.0 };
            public MediaPlayer Medicine = new MediaPlayer() { Volume = 1.0 };
            public MediaPlayer Phone = new MediaPlayer() { Volume = 1.0 };
            public MediaPlayer Adrenaline = new MediaPlayer() { Volume = 1.0 };
            public MediaPlayer Magazine = new MediaPlayer() { Volume = 1.0 };
            public MediaPlayer Gunpowder = new MediaPlayer() { Volume = 1.0 };
            public MediaPlayer Bullet = new MediaPlayer() { Volume = 1.0 };
            public MediaPlayer Trashbin = new MediaPlayer() { Volume = 1.0 };

            public void Media_Ended(object sender, EventArgs e)
            {
                (sender as MediaPlayer).Position = TimeSpan.Zero;
                (sender as MediaPlayer).Play();
            }

            public SoundLib()
            {
                Title.Open(new Uri("sounds/bsr_title.wav", UriKind.Relative));
                Background1.Open(new Uri("sounds/bsr_background1.wav", UriKind.Relative));
                Background2.Open(new Uri("sounds/bsr_background2.wav", UriKind.Relative));
                End.Open(new Uri("sounds/bsr_end.wav", UriKind.Relative));
                Empty.Open(new Uri("sounds/bsr_empty.wav", UriKind.Relative));
                Shot.Open(new Uri("sounds/bsr_shot.wav", UriKind.Relative));
                GunpowderShot.Open(new Uri("sounds/bsr_gunpowder_shot.wav", UriKind.Relative));
                Saw.Open(new Uri("sounds/bsr_saw.wav", UriKind.Relative));
                Magnify.Open(new Uri("sounds/bsr_magnify.wav", UriKind.Relative));
                Beer.Open(new Uri("sounds/bsr_beer.wav", UriKind.Relative));
                Cig.Open(new Uri("sounds/bsr_cig.wav", UriKind.Relative));
                Handcuff.Open(new Uri("sounds/bsr_handcuff.wav", UriKind.Relative));
                Inverter.Open(new Uri("sounds/bsr_inverter.wav", UriKind.Relative));
                Medicine.Open(new Uri("sounds/bsr_medicine.wav", UriKind.Relative));
                Phone.Open(new Uri("sounds/bsr_phone.wav", UriKind.Relative));
                Adrenaline.Open(new Uri("sounds/bsr_adrenaline.wav", UriKind.Relative));
                Magazine.Open(new Uri("sounds/bsr_magazine.wav", UriKind.Relative));
                Gunpowder.Open(new Uri("sounds/bsr_gunpowder.wav", UriKind.Relative));
                Bullet.Open(new Uri("sounds/bsr_bullet.wav", UriKind.Relative));
                Trashbin.Open(new Uri("sounds/bsr_trashbin.wav", UriKind.Relative));

                Title.MediaEnded += Media_Ended;
                Background1.MediaEnded += Media_Ended;
                Background2.MediaEnded += Media_Ended;
                End.MediaEnded += Media_Ended;
            }
        }

        private SoundLib Sound = new SoundLib();
    }
}