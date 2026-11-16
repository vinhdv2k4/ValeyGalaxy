#if false
using Microsoft.Xna.Framework;
using System.Collections.Generic;

namespace GameSource
{
  public class SchedulePathDescription
  {
    public Stack<Point> route;
    public int facingDirection;
    public string endOfRouteBehavior;
    public string endOfRouteMessage;

    public SchedulePathDescription(
      Stack<Point> route,
      int facingDirection,
      string endBehavior,
      string endMessage)
    {
      this.endOfRouteMessage = endMessage;
      this.route = route;
      this.facingDirection = facingDirection;
      this.endOfRouteBehavior = endBehavior;
    }
  }
}
#endif
