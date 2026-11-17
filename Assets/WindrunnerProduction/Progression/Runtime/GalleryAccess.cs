using System;

namespace Windrunner.Production.Progression
{
    public static class GalleryAccess
    {
        public static bool Unlocked(ProgressionState records,string cutsceneId) =>
            records?.cutscenes.Exists(c=>c.id==cutsceneId && (c.status==CinematicStatus.Watched || c.status==CinematicStatus.Skipped))==true;

        // Only a presentation callback crosses this boundary. It receives no writable progression authority.
        public static bool Replay(ProgressionState records,string id,Action play)
        {
            if(play==null || !Unlocked(records,id))return false;
            play();return true;
        }
    }
}
