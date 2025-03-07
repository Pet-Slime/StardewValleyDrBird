using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using CoreBoy;
using CoreBoy.gui;
using CoreBoy.memory.cart.battery;
using CoreBoy.serial;
using CoreBoy.sound;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewModdingAPI;
using StardewModdingAPI.Utilities;
using StardewValley;
using StardewValley.Minigames;

namespace GameboyArcade
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1001:Types that own disposable fields should be disposable", Justification = "Handled")]
    class GameboyMinigame : IMinigame
    {
        private const int GBWidth = 160;
        private const int GBHeight = 144;
        private const int UIWidth = GBWidth * 4;
        private const int UIHeight = GBHeight * 4;

        public Emulator Emulator;
        public Content Content;
        private CancellationTokenSource Cancellation;

        private static readonly PerScreen<ushort[]> NextFrame = new PerScreen<ushort[]>(() => new ushort[GBWidth * GBHeight]);
        private static readonly PerScreen<Texture2D> ScreenBuffer = new PerScreen<Texture2D>(() => new Texture2D(Game1.graphics.GraphicsDevice, GBWidth, GBHeight, false, SurfaceFormat.Bgra5551));
        private Rectangle ScreenArea = new Rectangle(0, 0, GBWidth, GBHeight);

        private Stopwatch FrameSw = new Stopwatch();
        private bool IsTurbo = false;

        private bool IsEvent = false;

        public GameboyMinigame(string romFile, Content content, bool isEvent = false)
        {
            this.SetScreenArea();
            this.Content = content;
            this.IsEvent = isEvent;
            GameboyOptions options = new GameboyOptions();
            options.Rom = romFile;
            switch (content.SaveStyle?.ToUpper())
            {
                case "LOCAL":
                    options.Battery = new GameboyLocalBattery(content.UniqueID);
                    break;
                case "GLOBAL":
                    options.Battery = new GameboyGlobalBattery(content.UniqueID);
                    break;
                case "SHARED":
                    options.Battery = new GameboySharedBattery(content.UniqueID);
                    break;
                default:
                    options.Battery = new NullBattery();
                    break;
            }

            this.Emulator = new Emulator(options);
            this.Emulator.Controller = new GameboyController(this);
            this.Emulator.Display = new BitmapDisplay();
            // TODO: Sound output is functioning but is incredibly choppy.
            // I assume this is because it's not synced with the CPU, and is instead relying on
            // Stardew threads to output sound async.  I have no idea how to fix this right
            // now, so I'm going to just disable by default.
            switch (content.SoundStyle?.ToUpper())
            {
                case "BROKEN":
                    this.Emulator.SoundOutput = new GameboySoundOutput();
                    break;
                default:
                    this.Emulator.SoundOutput = new NullSoundOutput();
                    break;
            }
            switch (content.LinkStyle?.ToUpper())
            {
                case "LOCAL":
                    this.Emulator.SerialEndpoint = new LocalSerialEndpoint();
                    break;
                case "REMOTE":
                    this.Emulator.SerialEndpoint = new RemoteSerialEndpoint(content.UniqueID);
                    break;
                default:
                    this.Emulator.SerialEndpoint = new NullSerialEndpoint();
                    break;
            }

            this.Emulator.Display.OnFrameProduced += this.BitmapDisplay_OnFrameProduced;

            this.Cancellation = new CancellationTokenSource();

            FrameSw.Start();
            this.Emulator.Run(this.Cancellation.Token);

        }
        public void BitmapDisplay_OnFrameProduced(object sender, ushort[] frameData)
        {
            ushort[] copy = (ushort[])frameData.Clone();
            // TODO: locking maybe?  Seems to work fine without...
            NextFrame.Value = copy;
            if (IsTurbo)
            {
                return;
            }
            Thread.Sleep(Math.Max(0, (int)(16 - FrameSw.ElapsedMilliseconds)));
            FrameSw.Restart();
        }

        public void changeScreenSize()
        {
            this.SetScreenArea();
        }

        private void SetScreenArea()
        {
            int XPos = (int)(Game1.viewport.Width * Game1.options.zoomLevel * (1 / Game1.options.uiScale)) / 2 - (UIWidth / 2);
            int YPos = (int)(Game1.viewport.Height * Game1.options.zoomLevel * (1 / Game1.options.uiScale)) / 2 - (UIHeight / 2);
            this.ScreenArea.X = XPos;
            this.ScreenArea.Y = YPos;
            this.ScreenArea.Width = (int)(1 / Game1.options.uiScale) * UIWidth;
            this.ScreenArea.Height = (int)(1 / Game1.options.uiScale) * UIHeight;
        }

        public bool doMainGameUpdates()
        {
            return false;
        }

        public void draw(SpriteBatch b)
        {
            ScreenBuffer.Value.SetData<ushort>(NextFrame.Value);

            b.Begin();
            b.Draw(ScreenBuffer.Value, this.ScreenArea, Color.White);
            // TODO: cursor has artifacts or is blurry?
            b.Draw(Game1.mouseCursors, new Vector2(Game1.getMouseX(), Game1.getMouseY()), new Rectangle(0, 0, 15, 15), Color.White, 0f, Vector2.Zero, 4f + Game1.dialogueButtonScale / 150f, SpriteEffects.None, 1f);
            b.End();
            
            if (this.IsEvent && Game1.activeClickableMenu != null)
            {
                Game1.PushUIMode();
                b.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp);
                Game1.activeClickableMenu.draw(b);
                b.End();
                Game1.PopUIMode();
            }
        }

        public bool forceQuit()
        {
            this.unload();
            return false;
        }

        public void leftClickHeld(int x, int y)
        {
            // do nothing
        }

        public string minigameId()
        {
            return this.Content.Name;
        }

        public bool overrideFreeMouseMovement()
        {
            return true;
        }

        /// <summary>
        /// Event Pokes are an integer, and represent a button press in a minigame
        /// </summary>
        /// <param name="data"></param>
        public void receiveEventPoke(int data)
        {
            if (Content.EnableEvents)
            {
                ((GameboyController)this.Emulator.Controller).ReceiveEventPoke(data);
            }
        }

        public void receiveKeyPress(Keys k)
        {
            if (this.IsEvent)
            {
                if (Game1.isQuestion)
                {
                    if (Game1.options.doesInputListContain(Game1.options.moveUpButton, k))
                    {
                        Game1.currentQuestionChoice = Math.Max(Game1.currentQuestionChoice - 1, 0);
                        Game1.playSound("toolSwap");
                    }
                    else if (Game1.options.doesInputListContain(Game1.options.moveDownButton, k))
                    {
                        Game1.currentQuestionChoice = Math.Min(Game1.currentQuestionChoice + 1, Game1.questionChoices.Count - 1);
                        Game1.playSound("toolSwap");
                    }
                }
                else if (Game1.activeClickableMenu != null)
                {
                    Game1.PushUIMode();
                    Game1.activeClickableMenu.receiveKeyPress(k);
                    Game1.PopUIMode();
                }
            }
        }

        public void receiveKeyRelease(Keys k)
        {
            // do nothing
        }
        public void receiveLeftClick(int x, int y, bool playSound = true)
        {
            if (this.IsEvent)
            {
                if (Game1.activeClickableMenu != null)
                {
                    Game1.PushUIMode();
                    Game1.activeClickableMenu.receiveLeftClick(x, y);
                    Game1.PopUIMode();
                }
            }
        }
        public void receiveRightClick(int x, int y, bool playSound = true)
        {
            if (this.IsEvent)
            {
                Game1.pressActionButton(Game1.GetKeyboardState(), Game1.input.GetMouseState(), Game1.input.GetGamePadState());
                if (Game1.activeClickableMenu != null)
                {
                    Game1.PushUIMode();
                    Game1.activeClickableMenu.receiveRightClick(x, y);
                    Game1.PopUIMode();
                }
            }
        }
        public void releaseLeftClick(int x, int y)
        {
            // do nothing
        }
        public void releaseRightClick(int x, int y)
        {
            // do nothing
        }
        public bool tick(GameTime time)
        {
            if (this.IsEvent)
            {
                Game1.currentLocation.currentEvent.checkForNextCommand(Game1.currentLocation, time);
                if (Game1.activeClickableMenu != null)
                {
                    Game1.PushUIMode();
                    Game1.activeClickableMenu.update(time);
                    Game1.PopUIMode();
                }
            }

            return false;
        }

        public void unload()
        {
            if (this.Emulator.Controller is IDisposable controller)
            {
                controller.Dispose();
            }
            if (this.Emulator.SerialEndpoint is IDisposable serialEndpoint)
            {
                serialEndpoint.Dispose();
            }
            this.Emulator.Stop(this.Cancellation);
            FrameSw.Reset();
            Game1.currentMinigame = null;
        }

        public void TurboToggle()
        {
            this.IsTurbo = !this.IsTurbo;
        }

        public static bool LoadGame(Content content, bool isEvent = false)
        {
            if (Game1.currentMinigame is not null || !Context.IsWorldReady)
            {
                return false;
            }
            if (!content.EnableEvents && isEvent)
            {
                return false;
            }
            string fullRomPath = Path.Combine(content.ContentPack.DirectoryPath, content.FilePath);

            Game1.currentMinigame = new GameboyMinigame(fullRomPath, content, isEvent);
            return true;
        }
    }
}
