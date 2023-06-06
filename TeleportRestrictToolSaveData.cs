using KSerialization;
using System.Runtime.Serialization;


namespace TeleportSuitMod
{
    [SerializationConfig(MemberSerialization.OptIn)]

    public class TeleportRestrictToolSaveData : KMonoBehaviour, ISaveLoadable
    {
        [Serialize]
        private bool[] TeleportRestrictSerialize = null;

        [OnSerializing]
        internal void OnSerializing()
        {
            this.TeleportRestrictSerialize=TeleportableOverlay.TeleportRestrict;
        }
        [OnDeserialized]
        internal void OnDeserialized()
        {
            TeleportableOverlay.TeleportRestrict=this.TeleportRestrictSerialize;
        }
    }
}
