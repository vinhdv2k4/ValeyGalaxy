#if false
namespace GameSource
{
  public class MailActivity : FarmActivity
  {
    public override bool AttemptActivity(Farm farm)
    {
      if (this._character.getSpouse() != null)
        this.activityPosition = Utility.PointToVector2(this._character.getSpouse().getMailboxPosition());
      else
        this.activityPosition = Utility.PointToVector2(Game1.MasterPlayer.getMailboxPosition());
      ++this.activityPosition.Y;
      this.activityDirection = 0;
      return true;
    }
  }
}
#endif
