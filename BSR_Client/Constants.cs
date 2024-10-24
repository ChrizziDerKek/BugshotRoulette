using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Reflection;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace BSR_Client
{
    public enum EMenuState
    {
        Startup,
        Join,
        Host,
        Settings,
        Gamestart,
        Gameover,
    }

    public enum EMusic
    {
        Undefined,
        Title,
        Background,
        BackgroundIntense,
        Gameover,
    }

    public enum EFlags
    {
        None = 0,
        Shooting = 1 << 0,
        UsingPlayerItem = 1 << 1,
        UsingAdrenaline = 1 << 2,
        UsingHeroine = 1 << 3,
        UsingKatana = 1 << 4,
        UsingSwapper = 1 << 5,
        NextItemTrashed = 1 << 6,
        HandcuffUsageBlocked = 1 << 7,
    }

    public class SettingsItem
    {
        public string ItemName { get; set; }

        public bool IsEnabled { get; set; }

        public SettingsItem(EItem item, bool enabled)
        {
            ItemName = item.ToString();
            IsEnabled = enabled;
        }
    }

    public class SoundLib
    {
        private readonly MediaPlayer Title = new MediaPlayer() { Volume = 0.05 };
        private readonly MediaPlayer Background1 = new MediaPlayer() { Volume = 0.05 };
        private readonly MediaPlayer Background2 = new MediaPlayer() { Volume = 0.05 };
        private readonly MediaPlayer End = new MediaPlayer() { Volume = 0.05 };
        private readonly MediaPlayer Empty = new MediaPlayer() { Volume = 1.0 };
        private readonly MediaPlayer Shot = new MediaPlayer() { Volume = 1.0 };
        private readonly MediaPlayer GunpowderShot = new MediaPlayer() { Volume = 1.0 };
        private readonly MediaPlayer Saw = new MediaPlayer() { Volume = 1.0 };
        private readonly MediaPlayer Magnify = new MediaPlayer() { Volume = 1.0 };
        private readonly MediaPlayer Beer = new MediaPlayer() { Volume = 1.0 };
        private readonly MediaPlayer Cig = new MediaPlayer() { Volume = 1.0 };
        private readonly MediaPlayer Handcuff = new MediaPlayer() { Volume = 1.0 };
        private readonly MediaPlayer Inverter = new MediaPlayer() { Volume = 1.0 };
        private readonly MediaPlayer Medicine = new MediaPlayer() { Volume = 1.0 };
        private readonly MediaPlayer Phone = new MediaPlayer() { Volume = 1.0 };
        private readonly MediaPlayer Adrenaline = new MediaPlayer() { Volume = 1.0 };
        private readonly MediaPlayer Magazine = new MediaPlayer() { Volume = 1.0 };
        private readonly MediaPlayer Gunpowder = new MediaPlayer() { Volume = 1.0 };
        private readonly MediaPlayer Bullet = new MediaPlayer() { Volume = 1.0 };
        private readonly MediaPlayer Trashbin = new MediaPlayer() { Volume = 1.0 };
        private readonly MediaPlayer Heroine = new MediaPlayer() { Volume = 1.0 };
        private readonly MediaPlayer Katana = new MediaPlayer() { Volume = 1.0 };
        private readonly MediaPlayer Swapper = new MediaPlayer() { Volume = 1.0 };
        private readonly MediaPlayer Hat = new MediaPlayer() { Volume = 1.0 };
        private bool WasPlaying = false;
        private EMusic Playing = EMusic.Undefined;

        public void PlayMusic(EMusic id)
        {
            if (id == Playing)
                return;
            Playing = id;
            if (!ShouldPlay())
                return;
            Title.MediaEnded -= Media_Ended;
            Background1.MediaEnded -= Media_Ended;
            Background2.MediaEnded -= Media_Ended;
            End.MediaEnded -= Media_Ended;
            Title.Stop();
            Background1.Stop();
            Background2.Stop();
            End.Stop();
            Title.MediaEnded += Media_Ended;
            Background1.MediaEnded += Media_Ended;
            Background2.MediaEnded += Media_Ended;
            End.MediaEnded += Media_Ended;
            switch (id)
            {
                case EMusic.Title:
                    Title.Play();
                    break;
                case EMusic.Background:
                    Background1.Play();
                    break;
                case EMusic.BackgroundIntense:
                    Background2.Play();
                    break;
                case EMusic.Gameover:
                    End.Play();
                    break;
            }
        }

        public void PlayShotSfx(EBullet bullet, EShotFlags flags)
        {
            if (!ShouldPlay())
                return;
            if (bullet == EBullet.Live)
            {
                if ((flags & EShotFlags.Gunpowdered) != 0)
                {
                    PlayOnce(GunpowderShot);
                    return;
                }
                PlayOnce(Shot);
                return;
            }
            PlayOnce(Empty);
        }

        public void PlayItemSfx(EItem item)
        {
            if (!ShouldPlay())
                return;
            switch (item)
            {
                case EItem.Handcuffs:
                    PlayOnce(Handcuff);
                    break;
                case EItem.Cigarettes:
                    PlayOnce(Cig);
                    break;
                case EItem.Saw:
                    PlayOnce(Saw);
                    break;
                case EItem.Magnifying:
                    PlayOnce(Magnify);
                    break;
                case EItem.Beer:
                    PlayOnce(Beer);
                    break;
                case EItem.Inverter:
                    PlayOnce(Inverter);
                    break;
                case EItem.Medicine:
                    PlayOnce(Medicine);
                    break;
                case EItem.Phone:
                    PlayOnce(Phone);
                    break;
                case EItem.Adrenaline:
                    PlayOnce(Adrenaline);
                    break;
                case EItem.Magazine:
                    PlayOnce(Magazine);
                    break;
                case EItem.Gunpowder:
                    PlayOnce(Gunpowder);
                    break;
                case EItem.Bullet:
                    PlayOnce(Bullet);
                    break;
                case EItem.Trashbin:
                    PlayOnce(Trashbin);
                    break;
                case EItem.Heroine:
                    PlayOnce(Heroine);
                    break;
                case EItem.Katana:
                    PlayOnce(Katana);
                    break;
                case EItem.Swapper:
                    PlayOnce(Swapper);
                    break;
                case EItem.Hat:
                    PlayOnce(Hat);
                    break;
            }
        }

        private bool IsOtherProcessRunning()
        {
            Process current = Process.GetCurrentProcess();
            Process[] processes = Process.GetProcessesByName(current.ProcessName);
            foreach (Process process in processes)
                if (process.Id != current.Id)
                    if (Assembly.GetExecutingAssembly().Location.Replace("/", "\\") == current.MainModule.FileName)
                        return true;
            return false;
        }

        private bool ShouldPlay()
        {
            if (IsOtherProcessRunning() && !WasPlaying)
                return false;
            WasPlaying = true;
            return true;
        }

        private void PlayOnce(MediaPlayer player)
        {
            player.Position = TimeSpan.Zero;
            player.Play();
        }

        private void Media_Ended(object sender, EventArgs e)
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
            Heroine.Open(new Uri("sounds/bsr_heroine.wav", UriKind.Relative));
            Katana.Open(new Uri("sounds/bsr_katana.wav", UriKind.Relative));
            Swapper.Open(new Uri("sounds/bsr_swapper.wav", UriKind.Relative));
            Hat.Open(new Uri("sounds/bsr_hat.wav", UriKind.Relative));
            Title.MediaEnded += Media_Ended;
            Background1.MediaEnded += Media_Ended;
            Background2.MediaEnded += Media_Ended;
            End.MediaEnded += Media_Ended;
        }
    }

    public partial class MainWindow
    {
        private readonly Dictionary<EItem, string> ItemDescriptions = new Dictionary<EItem, string>()
        {
            { EItem.Nothing, null },
            { EItem.Handcuffs, "Skips the enemy's turns so you can shoot 2 times" },
            { EItem.Cigarettes, "Restores 1 Health" },
            { EItem.Saw, "Saws off the shotgun's barrel so that it deals 2 damage\nCan be combined with gunpowder to deal 4 damage or 3 to yourself" },
            { EItem.Magnifying, "Shows you the bullet type that's currently loaded" },
            { EItem.Beer, "Racks the bullet that's currently loaded" },
            { EItem.Inverter, "Inverts the bullet type that's currently loaded\nA live becomes a blank and vice versa" },
            { EItem.Medicine, "Has a 50/50 chance of restoring 2 Health or losing 1" },
            { EItem.Phone, "Call a stranger who tells you the type of a random bullet" },
            { EItem.Adrenaline, "Select a player to look at their items and steal one of them to use it immediately\nStealing adrenaline isn't possible" },
            { EItem.Magazine, "Generates a new set of bullets" },
            { EItem.Gunpowder, "Has a 50/50 chance of dealing 3 damage or exploding in the barrel and dealing 2 to yourself\nCan be combined with a saw to deal 4 damage or 3 to yourself" },
            { EItem.Bullet, "Loads a new random bullet type into the gun\nAlways appears at the end of the round" },
            { EItem.Trashbin, "Allows you to throw away an item and receive a different one" },
            { EItem.Heroine, "Select a player to give them heroine\nThey can't use an item in their next round" },
            { EItem.Katana, "Select a player to cut their fingers off\nThey can only use 1 item in their next round" },
            { EItem.Swapper, "Swaps your items with the ones with the selected player" },
            { EItem.Hat, "Hides the bullets for every player" },
            { EItem.Count, null },
        };

        private readonly Rectangle[] BulletDisplays = null;
        private readonly ProgressBar[] HealthBars = null;
        private readonly Button[] ItemDisplays = null;
        private readonly Button[] PlayerDisplays = null;
        private ClientWorker Sync = null;
        private List<string> Players = new List<string>();
        private string Host = "";
        private string You = "";
        private string Session = "";
        private bool GameStarted = false;
        private EFlags Flags = EFlags.None;
        private bool PacketHandled = false;
        private readonly SoundLib Sound = new SoundLib();
    }
}