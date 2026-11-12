using UnityEngine;

namespace DontForgetMe
{
    // 在开场完成后扩建第二章灰盒：院外空地、窄路、服务区附属房与清晨的岔路口。
    public static class RoadWorld
    {
        private static readonly Color Stone = new Color(0.28f, 0.30f, 0.31f);
        private static readonly Color Metal = new Color(0.33f, 0.36f, 0.38f);
        private static readonly Color Paper = new Color(0.78f, 0.76f, 0.66f);

        public static void Extend(DemoGame game)
        {
            BuildGeometry(game);
            ConfigureActors(game);
            BuildFurnitureAndInteractions(game);
            BuildRoadsideDetails(game);
        }

        private static void BuildGeometry(DemoGame game)
        {
            // 院墙打开一道缺口，先由临时闸板挡着，两通电话打完才放行。
            Object.Destroy(game.NorthWall);
            OpeningWorld.Box("NorthWallLeft", new Vector3(-5.4f, 2, 72), new Vector3(7.2f, 4, 0.5f), Stone);
            OpeningWorld.Box("NorthWallRight", new Vector3(5.4f, 2, 72), new Vector3(7.2f, 4, 0.5f), Stone);
            game.NorthGate = OpeningWorld.Box("NorthGate", new Vector3(0, 1.35f, 72),
                new Vector3(3.6f, 2.7f, 0.2f), Metal);

            // 窄路：z 72—98。
            OpeningWorld.Box("PathGround", new Vector3(0, -0.2f, 85), new Vector3(4.8f, 0.4f, 26), OpeningWorld.Concrete);
            OpeningWorld.Box("PathWest", new Vector3(-2.1f, 2, 85), new Vector3(0.6f, 4, 26), Stone);
            OpeningWorld.Box("PathEast", new Vector3(2.1f, 2, 85), new Vector3(0.6f, 4, 26), Stone);
            OpeningWorld.Box("PathEdgeWest", new Vector3(-1.65f, 0.025f, 85), new Vector3(0.06f, 0.025f, 25),
                new Color(0.56f, 0.52f, 0.43f), null, false);
            OpeningWorld.Box("PathEdgeEast", new Vector3(1.65f, 0.025f, 85), new Vector3(0.06f, 0.025f, 25),
                new Color(0.56f, 0.52f, 0.43f), null, false);
            for (int i = 0; i < 3; i++)
                OpeningWorld.Box("GlassShard", new Vector3(-0.5f + i * 0.5f, 0.015f, 85.4f + (i % 2) * 0.6f),
                    new Vector3(0.14f, 0.03f, 0.1f), new Color(0.76f, 0.86f, 0.9f), null, false);

            // 服务区附属房：z 98—112。南门起初开着，夜里被风带上。
            OpeningWorld.Box("ShelterGround", new Vector3(0, -0.2f, 105), new Vector3(12, 0.4f, 14), OpeningWorld.Concrete);
            OpeningWorld.Box("ShelterWest", new Vector3(-6, 2, 105), new Vector3(0.5f, 4, 14.5f), Stone);
            OpeningWorld.Box("ShelterEast", new Vector3(6, 2, 105), new Vector3(0.5f, 4, 14.5f), Stone);
            OpeningWorld.Box("ShelterSouthLeft", new Vector3(-3.75f, 2, 98), new Vector3(4.5f, 4, 0.5f), Stone);
            OpeningWorld.Box("ShelterSouthRight", new Vector3(3.75f, 2, 98), new Vector3(4.5f, 4, 0.5f), Stone);
            game.ShelterDoor = OpeningWorld.Box("ShelterDoor", new Vector3(0, 4.5f, 98),
                new Vector3(3.2f, 2.5f, 0.2f), Metal);
            OpeningWorld.Box("ShelterNorthLeft", new Vector3(-3.75f, 2, 112), new Vector3(4.5f, 4, 0.5f), Stone);
            OpeningWorld.Box("ShelterNorthRight", new Vector3(3.75f, 2, 112), new Vector3(4.5f, 4, 0.5f), Stone);
            game.ShelterExit = OpeningWorld.Box("ShelterExit", new Vector3(0, 1.25f, 112),
                new Vector3(3.2f, 2.5f, 0.2f), Metal);
            OpeningWorld.Box("ShelterRoofBeamWest", new Vector3(-5.65f, 3.8f, 105),
                new Vector3(0.35f, 0.3f, 14), Metal);
            OpeningWorld.Box("ShelterRoofBeamEast", new Vector3(5.65f, 3.8f, 105),
                new Vector3(0.35f, 0.3f, 14), Metal);
            for (int i = 0; i < 5; i++)
                OpeningWorld.Box("ShelterRoofRib", new Vector3(-4.8f + i * 2.4f, 3.75f, 105),
                    new Vector3(0.11f, 0.16f, 13.5f), new Color(0.25f, 0.28f, 0.29f));

            // 清晨的岔路口：z 112—124。
            OpeningWorld.Box("ForkGround", new Vector3(0, -0.2f, 118.3f), new Vector3(22, 0.4f, 12.5f), Stone);
            OpeningWorld.Box("ForkNorth", new Vector3(0, 2, 124.4f), new Vector3(22, 4, 0.5f), Stone);
            OpeningWorld.Box("ForkWest", new Vector3(-11, 2, 118.3f), new Vector3(0.5f, 4, 13), Stone);
            OpeningWorld.Box("ForkEast", new Vector3(11, 2, 118.3f), new Vector3(0.5f, 4, 13), Stone);
        }

        private static void ConfigureActors(DemoGame game)
        {
            game.Woman.FollowTarget = game.Player.transform;
            game.Woman.FollowOffset = new Vector3(0.9f, 0, -1.2f);
            game.Dog.FollowTarget = game.Woman.transform;
            game.Dog.FollowOffset = new Vector3(-0.7f, 0, -0.7f);
            game.ResidentA.DisplayName = "小谭";
            game.ResidentA.FollowTarget = game.Player.transform;
            game.ResidentA.FollowOffset = new Vector3(-1.3f, 0, -1.6f);
            game.ResidentB.GoTo(new Vector3(-7, 0, 55));
            foreach (string companion in new[] { "player", "woman", "zhou", "dog" })
            {
                game.Memories.Recognize(companion, "xiaotan");
                game.Memories.Recognize("xiaotan", companion);
            }
            game.Zhou.GoTo(new Vector3(-1.6f, 0, 60));
        }

        private static void BuildFurnitureAndInteractions(DemoGame game)
        {
            GameObject map = OpeningWorld.Box("RouteMap", new Vector3(-5.6f, 1.7f, 105),
                new Vector3(0.15f, 1.1f, 1.7f), Paper, null, false);
            GameObject notice = OpeningWorld.Box("Notice", new Vector3(5.65f, 1.6f, 103),
                new Vector3(0.12f, 0.9f, 1.2f), Paper, null, false);
            GameObject dispenser = OpeningWorld.Box("WaterDispenser", new Vector3(5.3f, 0.8f, 109.5f),
                new Vector3(0.6f, 1.6f, 0.6f), Metal);
            GameObject table = OpeningWorld.Box("ShelterTable", new Vector3(1.8f, 0.45f, 106.4f),
                new Vector3(1.7f, 0.9f, 1f), OpeningWorld.Warm);
            GameObject bunk = null;
            for (int i = 0; i < 4; i++)
            {
                float z = 99.4f + i * 1.6f;
                bunk = OpeningWorld.Box("Bunk", new Vector3(-4.5f, 0.15f, 99.4f + i * 1.6f),
                    new Vector3(2f, 0.3f, 1.3f), OpeningWorld.Warm * 0.75f);
                OpeningWorld.Box("BunkBlanket", new Vector3(-4.5f, 0.34f, z),
                    new Vector3(1.8f, 0.08f, 1.15f), i % 2 == 0
                        ? new Color(0.34f, 0.39f, 0.38f) : new Color(0.43f, 0.35f, 0.29f),
                    null, false);
                OpeningWorld.Box("BunkPillow", new Vector3(-5.05f, 0.4f, z + 0.37f),
                    new Vector3(0.55f, 0.14f, 0.4f), Paper, null, false);
            }

            OpeningWorld.Box("RouteMapFrameTop", new Vector3(-5.6f, 2.3f, 105),
                new Vector3(0.2f, 0.08f, 1.85f), Metal, null, false);
            OpeningWorld.Box("RouteMapFrameBottom", new Vector3(-5.6f, 1.1f, 105),
                new Vector3(0.2f, 0.08f, 1.85f), Metal, null, false);
            OpeningWorld.Box("MapRoute", new Vector3(-5.49f, 1.63f, 105),
                new Vector3(0.025f, 0.035f, 1.12f), new Color(0.67f, 0.34f, 0.25f), null, false);
            OpeningWorld.Box("NoticeClip", new Vector3(5.52f, 2.08f, 103),
                new Vector3(0.2f, 0.18f, 0.2f), Metal, null, false);
            OpeningWorld.Box("TablePaper", new Vector3(1.8f, 0.94f, 106.25f),
                new Vector3(0.72f, 0.025f, 0.55f), Paper, null, false);
            OpeningWorld.Box("TableRadio", new Vector3(2.3f, 1.02f, 106.65f),
                new Vector3(0.34f, 0.18f, 0.25f), new Color(0.2f, 0.24f, 0.24f), null, false);
            OpeningWorld.Box("RadioAntenna", new Vector3(2.35f, 1.25f, 106.65f),
                new Vector3(0.025f, 0.3f, 0.025f), Metal, null, false);

            OpeningWorld.Box("DispenserCup", new Vector3(5.25f, 1.66f, 109.2f),
                new Vector3(0.19f, 0.24f, 0.19f), Paper, null, false);
            OpeningWorld.Box("ShelterDoorKickplate", new Vector3(0, 0.35f, 97.88f),
                new Vector3(2.6f, 0.45f, 0.045f), new Color(0.23f, 0.26f, 0.27f), null, false);
            OpeningWorld.Box("ForkSignPost", new Vector3(8.7f, 1.55f, 117.4f),
                new Vector3(0.16f, 3.1f, 0.16f), Metal, null, false);
            OpeningWorld.Box("ForkSign", new Vector3(8.7f, 2.65f, 117.4f),
                new Vector3(1.15f, 0.64f, 0.16f), new Color(0.34f, 0.4f, 0.35f), null, false);
            OpeningWorld.Box("ForkSignArrow", new Vector3(8.7f, 2.65f, 117.29f),
                new Vector3(0.54f, 0.06f, 0.025f), Paper, null, false);

            GameObject lampShade = OpeningWorld.Shape(PrimitiveType.Sphere, "ShelterLampShade",
                new Vector3(0, 3.3f, 105), new Vector3(0.5f, 0.18f, 0.5f),
                new Color(0.73f, 0.63f, 0.43f), null);
            game.ShelterLamp = lampShade.AddComponent<Light>();
            game.ShelterLamp.type = LightType.Point;
            game.ShelterLamp.color = new Color(1f, 0.72f, 0.44f);
            game.ShelterLamp.range = 10f;
            game.ShelterLamp.intensity = 1.2f;
            game.ShelterLamp.enabled = false;

            JourneyState journey = game.Journey;
            game.AddInteraction(game.Zhou.gameObject, "陪老周给家里打电话",
                () => journey.Phase == JourneyPhase.Calls && !journey.CalledFamily, game.CallFamily);
            game.AddInteraction(game.Woman.gameObject, "陪她给母亲打电话",
                () => journey.Phase == JourneyPhase.Calls && !journey.CalledMother, game.CallMother);
            game.AddInteraction(game.Dog.gameObject, "蹲下，让豆子先闻闻你的手",
                () => journey.Phase == JourneyPhase.Path && game.PathSceneStarted && !journey.DogCalmed,
                game.CalmDog);
            game.AddInteraction(game.Dog.gameObject, "把爪垫里的碎玻璃挑出来",
                () => journey.Phase == JourneyPhase.Path && journey.DogCalmed && !journey.GlassRemoved,
                game.RemoveGlass);
            game.AddInteraction(game.Zhou.gameObject, "劝老周留下：你答应她别硬走",
                () => journey.Phase == JourneyPhase.Shelter && !journey.ZhouPersuaded, game.PersuadeZhou);
            game.AddInteraction(game.ShelterDoor, "和老周一起调整闭门器",
                () => journey.Phase == JourneyPhase.Night && game.DoorSlammed && !journey.DoorFixed,
                game.FixDoor);
            game.AddInteraction(game.Zhou.gameObject, "和老周道别",
                () => journey.Phase == JourneyPhase.Morning && journey.AddressCopied && !game.FarewellStarted,
                game.BeginFarewell);

            game.AddInteraction(bunk, "招呼大家休息",
                () => journey.Phase == JourneyPhase.Shelter && journey.ZhouPersuaded, game.RestForNight);
            game.AddInteraction(bunk, "睡到天亮",
                () => journey.Phase == JourneyPhase.Night && journey.DoorFixed, game.SleepUntilMorning);

            game.AddInteraction(table, "把柳河地址抄进路线册",
                () => journey.Phase == JourneyPhase.Morning && !journey.AddressCopied, game.CopyAddress);

            game.AddInteraction(dispenser, "按一下饮水机（可选）",
                () => journey.Phase == JourneyPhase.Shelter && !journey.SharedWater, game.ShareWater);

            game.AddInteraction(map, "看一眼路线图（可选）",
                () => journey.Phase == JourneyPhase.Shelter || journey.Phase == JourneyPhase.Morning,
                () => game.Caption("到柳河的步行路线原估约十天，包含绕行与休整。\n老周用铅笔在几个点上画了圈，又算了一遍剩下的步程。", 7));

            game.AddInteraction(notice, "看小谭找到的告示（可选）",
                () => journey.Phase == JourneyPhase.Shelter || journey.Phase == JourneyPhase.Night
                    || journey.Phase == JourneyPhase.Morning,
                () => game.Caption("告示：柳河仍接收转移人员，道路情况每日更新。\n小谭：那就还来得及。", 6));
        }

        private static void BuildRoadsideDetails(DemoGame game)
        {
            Color puddle = new Color(0.28f, 0.36f, 0.38f);
            Color fadedPaint = new Color(0.62f, 0.58f, 0.49f);
            for (int i = 0; i < 4; i++)
            {
                float z = 76f + i * 5.5f;
                OpeningWorld.Box("PathDistanceMark", new Vector3(0, 0.015f, z),
                    new Vector3(0.07f, 0.018f, 0.8f), fadedPaint, null, false);
            }

            OpeningWorld.Shape(PrimitiveType.Cylinder, "PathPuddleA", new Vector3(-1.2f, 0.012f, 91),
                new Vector3(0.72f, 0.012f, 0.3f), puddle, null);
            OpeningWorld.Shape(PrimitiveType.Cylinder, "PathPuddleB", new Vector3(1.2f, 0.012f, 96),
                new Vector3(0.45f, 0.012f, 0.22f), puddle, null);
            OpeningWorld.Box("ShelterCanopy", new Vector3(0, 3.95f, 96.9f),
                new Vector3(7.8f, 0.18f, 2.2f), Metal);
            OpeningWorld.Box("CanopySupportWest", new Vector3(-3.4f, 1.9f, 97.8f),
                new Vector3(0.12f, 3.8f, 0.12f), Metal);
            OpeningWorld.Box("CanopySupportEast", new Vector3(3.4f, 1.9f, 97.8f),
                new Vector3(0.12f, 3.8f, 0.12f), Metal);

            OpeningWorld.Box("ShelterPipe", new Vector3(5.55f, 1.6f, 100),
                new Vector3(0.09f, 3.2f, 0.09f), new Color(0.35f, 0.37f, 0.35f), null, false);
            OpeningWorld.Box("ShelterPipeBend", new Vector3(5.35f, 0.09f, 100),
                new Vector3(0.5f, 0.09f, 0.09f), new Color(0.35f, 0.37f, 0.35f), null, false);

            OpeningWorld.Box("ForkRouteWest", new Vector3(-4.5f, 0.015f, 118),
                new Vector3(0.08f, 0.018f, 2.2f), fadedPaint, null, false);
            OpeningWorld.Box("ForkRouteEast", new Vector3(4.5f, 0.015f, 118),
                new Vector3(0.08f, 0.018f, 2.2f), fadedPaint, null, false);
        }
    }
}
