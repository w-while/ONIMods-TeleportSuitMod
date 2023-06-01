using System;
using System.Collections.Generic;
using UnityEngine;
using static Components;
using static STRINGS.BUILDINGS;
using static STRINGS.UI.STARMAP;

namespace TeleportSuitMod
{
    //该类用于维护一个世界中是否有穿着传送服的小人，用于更新殖民地能拿到的所有掉落物/库存
    public class TeleportSuitWorldCountManager : KMonoBehaviour, ISim1000ms
    {
        public static TeleportSuitWorldCountManager Instance;

        //一个很让我困惑的点在于在引用时(Instance==null)为真，但是直接使用不会触发为空报错

        public Dictionary<int, int> WorldCount = new Dictionary<int, int>();
        private int[] innerTempList = new int[50];
        private int len;

        public void Sim1000ms(float dt)
        {
            if (WorldCount==null)
            {
                return;
            }
            len=WorldCount.Keys.Count;
            if (innerTempList.Length<len)
            {
                innerTempList=new int[len*2];
            }
            WorldCount.Keys.CopyTo(innerTempList, 0);
            for (int i = 0; i<len; i++)
            {
                WorldCount[innerTempList[i]]=0;
            }
            foreach (MinionResume item in Components.MinionResumes.Items)
            {
                if (item.HasTag(GameTags.Dead))
                {
                    continue;
                }
                Navigator navigator = item.GetComponent<Navigator>();
                if (navigator==null)
                {
                    continue;
                }
                if ((navigator.flags&TeleportSuitConfig.TeleportSuitFlags)==0)//没有传送服
                {
                    continue;
                }
                if (WorldCount.ContainsKey(item.GetMyParentWorldId()))
                {
                    WorldCount[item.GetMyParentWorldId()]++;
                }
                else
                {
                    WorldCount[item.GetMyParentWorldId()]=1;
                }
            }
        }
        protected override void OnPrefabInit()
        {
            base.OnPrefabInit();
            Instance = this;
        }
    }
}
