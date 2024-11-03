using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Threading.Tasks;
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
        Credits,
    }

    public enum EFlags
    {
        None = 0,
        Shooting = 1 << 0,
        UsingPlayerItem = 1 << 1,
        UsingAdrenaline = 1 << 2,      
        NextItemTrashed = 1 << 3,
        HandcuffUsageBlocked = 1 << 4,
        ItemUsageBlockedCompletely = 1 << 5,
        AdrenalinePending = 1 << 6,
        GameEnded = 1 << 7,
        ItemUsageBlockedPartially = 1 << 8,
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
        private readonly MediaPlayer Title = new MediaPlayer() { Volume = 0.0 };
        private readonly MediaPlayer Background1 = new MediaPlayer() { Volume = 0.0 };
        private readonly MediaPlayer Background2 = new MediaPlayer() { Volume = 0.0 };
        private readonly MediaPlayer Background3 = new MediaPlayer() { Volume = 0.0 };
        private readonly MediaPlayer Background4 = new MediaPlayer() { Volume = 0.0 };
        private readonly MediaPlayer End = new MediaPlayer() { Volume = 0.0 };
        private readonly MediaPlayer Empty = new MediaPlayer() { Volume = 0.0 };
        private readonly MediaPlayer Shot = new MediaPlayer() { Volume = 0.0 };
        private readonly MediaPlayer GunpowderShot = new MediaPlayer() { Volume = 0.0 };
        private readonly MediaPlayer Saw = new MediaPlayer() { Volume = 0.0 };
        private readonly MediaPlayer Magnify = new MediaPlayer() { Volume = 0.0 };
        private readonly MediaPlayer Beer = new MediaPlayer() { Volume = 0.0 };
        private readonly MediaPlayer Cig = new MediaPlayer() { Volume = 0.0 };
        private readonly MediaPlayer Handcuff = new MediaPlayer() { Volume = 0.0 };
        private readonly MediaPlayer Inverter = new MediaPlayer() { Volume = 0.0 };
        private readonly MediaPlayer Medicine = new MediaPlayer() { Volume = 0.0 };
        private readonly MediaPlayer Phone = new MediaPlayer() { Volume = 0.0 };
        private readonly MediaPlayer Adrenaline = new MediaPlayer() { Volume = 0.0 };
        private readonly MediaPlayer Magazine = new MediaPlayer() { Volume = 0.0 };
        private readonly MediaPlayer Gunpowder = new MediaPlayer() { Volume = 0.0 };
        private readonly MediaPlayer Bullet = new MediaPlayer() { Volume = 0.0 };
        private readonly MediaPlayer Trashbin = new MediaPlayer() { Volume = 0.0 };
        private readonly MediaPlayer Heroine = new MediaPlayer() { Volume = 0.0 };
        private readonly MediaPlayer Katana = new MediaPlayer() { Volume = 0.0 };
        private readonly MediaPlayer Swapper = new MediaPlayer() { Volume = 0.0 };
        private readonly MediaPlayer Hat = new MediaPlayer() { Volume = 0.0 };
        private readonly MediaPlayer Snus = new MediaPlayer() { Volume = 0.0 };
        private readonly MediaPlayer Elfbar = new MediaPlayer() { Volume = 0.0 };
        private readonly MediaPlayer Scope = new MediaPlayer() { Volume = 0.0 };
        private readonly MediaPlayer Remote = new MediaPlayer() { Volume = 0.0 };
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
            Background3.MediaEnded -= Media_Ended;
            Background4.MediaEnded -= Media_Ended;
            End.MediaEnded -= Media_Ended;
            Title.Stop();
            Background1.Stop();
            Background2.Stop();
            Background3.Stop();
            Background4.Stop();
            End.Stop();
            Title.MediaEnded += Media_Ended;
            Background1.MediaEnded += Media_Ended;
            Background2.MediaEnded += Media_Ended;
            Background3.MediaEnded += Media_Ended;
            Background4.MediaEnded += Media_Ended;
            End.MediaEnded += Media_Ended;
            switch (id)
            {
                case EMusic.Title:
                    Title.Play();
                    break;
                case EMusic.BackgroundClassic:
                    Background1.Play();
                    break;
                case EMusic.BackgroundIntense:
                    Background2.Play();
                    break;
                case EMusic.Gameover:
                    End.Play();
                    break;
                case EMusic.BackgroundBearing:
                    Background3.Play();
                    break;
                case EMusic.BackgroundJungle:
                    Background4.Play();
                    break;
            }
        }

        public void PlayShotSfx(EBullet bullet, ERoundFlags flags)
        {
            if (!ShouldPlay())
                return;
            if (bullet == EBullet.Live)
            {
                if ((flags & ERoundFlags.ShotGunpowdered) != 0)
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
                case EItem.Snus:
                    PlayOnce(Snus);
                    break;
                case EItem.Elfbar:
                    PlayOnce(Elfbar);
                    break;
                case EItem.Scope:
                    PlayOnce(Scope);
                    break;
                case EItem.Remote:
                    PlayOnce(Remote);
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

        public SoundLib(MainWindow w)
        {
            w.Dispatcher.Invoke(() =>
            {
                Title.Open(new Uri("sounds/bsr_title.mp3", UriKind.Relative));
                Background1.Open(new Uri("sounds/bsr_background1.mp3", UriKind.Relative));
                Background2.Open(new Uri("sounds/bsr_background2.mp3", UriKind.Relative));
                Background3.Open(new Uri("sounds/bsr_background3.mp3", UriKind.Relative));
                Background4.Open(new Uri("sounds/bsr_background4.mp3", UriKind.Relative));
                End.Open(new Uri("sounds/bsr_end.mp3", UriKind.Relative));
                Empty.Open(new Uri("sounds/bsr_empty.mp3", UriKind.Relative));
                Shot.Open(new Uri("sounds/bsr_shot.mp3", UriKind.Relative));
                GunpowderShot.Open(new Uri("sounds/bsr_gunpowder_shot.mp3", UriKind.Relative));
                Saw.Open(new Uri("sounds/bsr_saw.mp3", UriKind.Relative));
                Magnify.Open(new Uri("sounds/bsr_magnify.mp3", UriKind.Relative));
                Beer.Open(new Uri("sounds/bsr_beer.mp3", UriKind.Relative));
                Cig.Open(new Uri("sounds/bsr_cig.mp3", UriKind.Relative));
                Handcuff.Open(new Uri("sounds/bsr_handcuff.mp3", UriKind.Relative));
                Inverter.Open(new Uri("sounds/bsr_inverter.mp3", UriKind.Relative));
                Medicine.Open(new Uri("sounds/bsr_medicine.mp3", UriKind.Relative));
                Phone.Open(new Uri("sounds/bsr_phone.mp3", UriKind.Relative));
                Adrenaline.Open(new Uri("sounds/bsr_adrenaline.mp3", UriKind.Relative));
                Magazine.Open(new Uri("sounds/bsr_magazine.mp3", UriKind.Relative));
                Gunpowder.Open(new Uri("sounds/bsr_gunpowder.mp3", UriKind.Relative));
                Bullet.Open(new Uri("sounds/bsr_bullet.mp3", UriKind.Relative));
                Trashbin.Open(new Uri("sounds/bsr_trashbin.mp3", UriKind.Relative));
                Heroine.Open(new Uri("sounds/bsr_heroine.mp3", UriKind.Relative));
                Katana.Open(new Uri("sounds/bsr_katana.mp3", UriKind.Relative));
                Swapper.Open(new Uri("sounds/bsr_swapper.mp3", UriKind.Relative));
                Hat.Open(new Uri("sounds/bsr_hat.mp3", UriKind.Relative));
                Snus.Open(new Uri("sounds/bsr_snus.mp3", UriKind.Relative));
                Elfbar.Open(new Uri("sounds/bsr_elfbar.mp3", UriKind.Relative));
                Scope.Open(new Uri("sounds/bsr_scope.mp3", UriKind.Relative));
                Remote.Open(new Uri("sounds/bsr_remote.mp3", UriKind.Relative));
                Title.MediaEnded += Media_Ended;
                Background1.MediaEnded += Media_Ended;
                Background2.MediaEnded += Media_Ended;
                Background3.MediaEnded += Media_Ended;
                Background4.MediaEnded += Media_Ended;
                End.MediaEnded += Media_Ended;
                Task.Delay(1000).Wait();
                Title.Volume = 0.05;
                Background1.Volume = 0.05;
                Background2.Volume = 0.05;
                Background3.Volume = 0.05;
                Background4.Volume = 0.05;
                End.Volume = 0.05;
                Empty.Volume = 1.0;
                Shot.Volume = 1.0;
                GunpowderShot.Volume = 1.0;
                Saw.Volume = 1.0;
                Magnify.Volume = 1.0;
                Beer.Volume = 1.0;
                Cig.Volume = 1.0;
                Handcuff.Volume = 1.0;
                Inverter.Volume = 1.0;
                Medicine.Volume = 1.0;
                Phone.Volume = 1.0;
                Adrenaline.Volume = 1.0;
                Magazine.Volume = 1.0;
                Gunpowder.Volume = 1.0;
                Bullet.Volume = 1.0;
                Trashbin.Volume = 1.0;
                Heroine.Volume = 1.0;
                Katana.Volume = 1.0;
                Swapper.Volume = 1.0;
                Hat.Volume = 1.0;
                Snus.Volume = 1.0;
                Elfbar.Volume = 1.0;
                Scope.Volume = 1.0;
                Remote.Volume = 1.0;
                PlayMusic(EMusic.Title);
            });
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
            { EItem.Snus, "Heals 2 health every round until you get shot" },
            { EItem.Elfbar, "Increases the maximum health amount by 1" },
            { EItem.Scope, "Tells you what the next 3 bullets are but the info might not be correct" },
            { EItem.Remote, "Inverts the player order" },
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
        private readonly EItem[] ItemStorage = new EItem[8];
        private bool AreItemsStored = false;
        private readonly SoundLib Sound = null;
        private EItem LastUsedItem = EItem.Nothing;
    }
}