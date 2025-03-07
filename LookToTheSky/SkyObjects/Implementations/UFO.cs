using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;

namespace LookToTheSky
{
    class UFO : SkyObject
    {

        public UFO(int yPos, bool moveRight) : base(new TemporaryAnimatedSprite(), yPos, moveRight)
        {
            this.Sprite.texture = Game1.content.Load<Texture2D>("LooseSprites\\Cursors");
            this.Sprite.sourceRect = new Rectangle(640, 784, 16, 16);
            this.Sprite.sourceRectStartingPos = new Vector2(this.Sprite.sourceRect.X, this.Sprite.sourceRect.Y);
            this.Sprite.interval = 100f;
            this.Sprite.animationLength = 4;
            this.Sprite.motion = new Vector2(moveRight ? 3f : -3f, 0f);
        }

        public override StardewValley.Object GetDropItem(int type = 0)
        {
            return new StardewValley.Object(337, 1);
        }

        public override bool OnHit(StardewValley.Object ammo)
        {
            Game1.playSound("UFO");
            this.Sprite.motion = new Vector2(0, -10f);
            this.DropItem();
            return true;
        }

        public override void OnEnter()
        {
            if (ModEntry.Config.DoNotificationNoise)
            {
                Game1.playSound("UFO");
            }
        }
    }
}
