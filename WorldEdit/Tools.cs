using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using OTAPI.Tile;
using Terraria;
using TShockAPI;
using TShockAPI.DB;
using Terraria.GameContent.Tile_Entities;
using Terraria.DataStructures;
using Microsoft.Xna.Framework;
using Terraria.ID;

namespace WorldEdit
{
    public static class Tools
    {
        internal const int BUFFER_SIZE = 1048576;
        internal static int MAX_UNDOS;

        public static bool InMapBoundaries(int X, int Y) =>
            ((X >= 0) && (Y >= 0) && (X < Main.maxTilesX) && (Y < Main.maxTilesY));

        private static readonly char[] InvalidFileNameChars = Path.GetInvalidFileNameChars();

        public static string GetClipboardPath(int accountID)
        {
            return Path.Combine(WorldEdit.WorldEditFolderName, string.Format("clipboard-{0}.dat", accountID));
        }
        public static bool IsCorrectName(string name)
        {
            return name.All(c => !InvalidFileNameChars.Contains(c));
        }
        public static List<int> GetColorID(string color)
        {
            int ID;
            if (int.TryParse(color, out ID) && ID >= 0 && ID < Main.numTileColors)
                return new List<int> { ID };

            var list = new List<int>();
            foreach (var kvp in WorldEdit.Colors)
            {
                if (kvp.Key == color)
                    return new List<int> { kvp.Value };
                if (kvp.Key.StartsWith(color))
                    list.Add(kvp.Value);
            }
            return list;
        }
        public static List<int> GetTileID(string tile)
        {
            int ID;
            if (int.TryParse(tile, out ID) && ID >= 0 && ID < Main.maxTileSets)
                return new List<int> { ID };

            var list = new List<int>();
            foreach (var kvp in WorldEdit.Tiles)
            {
                if (kvp.Key == tile)
                    return new List<int> { kvp.Value };
                if (kvp.Key.StartsWith(tile))
                    list.Add(kvp.Value);
            }
            return list;
        }
        public static List<int> GetWallID(string wall)
        {
            int ID;
            if (int.TryParse(wall, out ID) && ID >= 0 && ID < Main.maxWallTypes)
                return new List<int> { ID };

            var list = new List<int>();
            foreach (var kvp in WorldEdit.Walls)
            {
                if (kvp.Key == wall)
                    return new List<int> { kvp.Value };
                if (kvp.Key.StartsWith(wall))
                    list.Add(kvp.Value);
            }
            return list;
        }
        public static int GetSlopeID(string slope)
        {
            int ID;
            if (int.TryParse(slope, out ID) && ID >= 0 && ID < 6)
                return ID;

            if (!WorldEdit.Slopes.TryGetValue(slope, out int Slope)) { return -1; }

            return Slope;
        }

        public static bool HasClipboard(int accountID)
        {
            return File.Exists(GetClipboardPath(accountID));
        }

        #region LoadWorldSectionData

        public static WorldSectionData LoadWorldData(Stream stream)
        {
            using (var reader = new BinaryReader(new BufferedStream(new GZipStream(stream,
                CompressionMode.Decompress), BUFFER_SIZE)))
            {
                var x = reader.ReadInt32();
                var y = reader.ReadInt32();
                var width = reader.ReadInt32();
                var height = reader.ReadInt32();
                var worldData = new WorldSectionData(width, height) { X = x, Y = y };

                for (var i = 0; i < width; i++)
                {
                    for (var j = 0; j < height; j++)
                        worldData.Tiles[i, j] = reader.ReadTile();
                }

                try
                {
                    var signCount = reader.ReadInt32();
                    worldData.Signs = new WorldSectionData.SignData[signCount];
                    for (var i = 0; i < signCount; i++)
                    {
                        worldData.Signs[i] = reader.ReadSign();
                    }

                    var chestCount = reader.ReadInt32();
                    worldData.Chests = new WorldSectionData.ChestData[chestCount];
                    for (var i = 0; i < chestCount; i++)
                    {
                        worldData.Chests[i] = reader.ReadChest();
                    }

                    var itemFrameCount = reader.ReadInt32();
                    worldData.ItemFrames = new WorldSectionData.ItemFrameData[itemFrameCount];
                    for (var i = 0; i < itemFrameCount; i++)
                    {
                        worldData.ItemFrames[i] = reader.ReadItemFrame();
                    }
                }
                catch (EndOfStreamException) // old version file
                { }

                try
                {
                    var logicSensorCount = reader.ReadInt32();
                    worldData.LogicSensors = new WorldSectionData.LogicSensorData[logicSensorCount];
                    for (var i = 0; i < logicSensorCount; i++)
                    {
                        worldData.LogicSensors[i] = reader.ReadLogicSensor();
                    }

                    var trainingDummyCount = reader.ReadInt32();
                    worldData.TrainingDummies = new WorldSectionData.TrainingDummyData[trainingDummyCount];
                    for (var i = 0; i < trainingDummyCount; i++)
                    {
                        worldData.TrainingDummies[i] = reader.ReadTrainingDummy();
                    }
                }
                catch (EndOfStreamException) // old version file
                { }

                return worldData;
            }
        }

        public static WorldSectionData LoadWorldData(string path) =>
            LoadWorldData(File.Open(path, FileMode.Open));

        private static Tile ReadTile(this BinaryReader reader)
        {
            var tile = new Tile
            {
                sTileHeader = reader.ReadInt16(),
                bTileHeader = reader.ReadByte(),
                bTileHeader2 = reader.ReadByte()
            };

            // Tile type
            if (tile.active())
            {
                tile.type = reader.ReadUInt16();
                if (Main.tileFrameImportant[tile.type])
                {
                    tile.frameX = reader.ReadInt16();
                    tile.frameY = reader.ReadInt16();
                }
            }
            tile.wall = reader.ReadUInt16();
            tile.liquid = reader.ReadByte();
            return tile;
        }

        private static WorldSectionData.SignData ReadSign(this BinaryReader reader)
        {
            return new WorldSectionData.SignData
            {
                X = reader.ReadInt32(),
                Y = reader.ReadInt32(),
                Text = reader.ReadString()
            };
        }

        private static WorldSectionData.ChestData ReadChest(this BinaryReader reader)
        {
            var x = reader.ReadInt32();
            var y = reader.ReadInt32();

            var count = reader.ReadInt32();
            var items = new NetItem[count];

            for (var i = 0; i < count; i++)
            {
                items[i] = new NetItem(reader.ReadInt32(), reader.ReadInt32(), reader.ReadByte());
            }

            return new WorldSectionData.ChestData
            {
                Items = items,
                X = x,
                Y = y
            };
        }

        private static WorldSectionData.ItemFrameData ReadItemFrame(this BinaryReader reader)
        {
            return new WorldSectionData.ItemFrameData
            {
                X = reader.ReadInt32(),
                Y = reader.ReadInt32(),
                Item = new NetItem(reader.ReadInt32(), reader.ReadInt32(), reader.ReadByte())
            };
        }

        private static WorldSectionData.LogicSensorData ReadLogicSensor(this BinaryReader reader)
        {
            return new WorldSectionData.LogicSensorData
            {
                X = reader.ReadInt32(),
                Y = reader.ReadInt32(),
                Type = (TELogicSensor.LogicCheckType)reader.ReadInt32()
            };
        }

        private static WorldSectionData.TrainingDummyData ReadTrainingDummy(this BinaryReader reader)
        {
            return new WorldSectionData.TrainingDummyData
            {
                X = reader.ReadInt32(),
                Y = reader.ReadInt32()
            };
        }

        #endregion

        public static int ClearSigns(int x, int y, int x2, int y2, bool emptyOnly)
        {
            int signs = 0;
            Rectangle area = new Rectangle(x, y, x2 - x, y2 - y);
            foreach (Sign sign in Main.sign)
            {
                if (sign == null) continue;
                if (area.Contains(sign.x, sign.y)
                    && (!emptyOnly || string.IsNullOrWhiteSpace(sign.text)))
                {
                    signs++;
                    Sign.KillSign(sign.x, sign.y);
                }
            }
            return signs;
        }

        public static int ClearChests(int x, int y, int x2, int y2, bool emptyOnly)
        {
            int chests = 0;
            Rectangle area = new Rectangle(x, y, x2 - x, y2 - y);
            foreach (Chest chest in Main.chest)
            {
                if (chest == null) continue;
                if (area.Contains(chest.x, chest.y)
                    && (!emptyOnly || chest.item.All(i => (i?.netID == 0))))
                {
                    chests++;
                    Chest.DestroyChest(chest.x, chest.y);
                }
            }
            return chests;
        }

        public static void ClearObjects(int x, int y, int x2, int y2)
        {
            ClearSigns(x, y, x2, y2, false);
            ClearChests(x, y, x2, y2, false);
            for (int i = x; i <= x2; i++)
            {
                for (int j = y; j <= y2; j++)
                {
                    if (TEItemFrame.Find(i, j) != -1)
                    { TEItemFrame.Kill(i, j); }
                    if (TELogicSensor.Find(i, j) != -1)
                    { TELogicSensor.Kill(i, j); }
                    if (TETrainingDummy.Find(i, j) != -1)
                    { TETrainingDummy.Kill(i, j); }
                }
            }
        }

        public static void LoadWorldSection(string path, int? X = null, int? Y = null, bool Tiles = true) =>
            LoadWorldSection(LoadWorldData(path), X, Y, Tiles);

        public static void LoadWorldSection(WorldSectionData Data, int? X = null, int? Y = null, bool Tiles = true)
		{
            int x = (X ?? Data.X), y = (Y ?? Data.Y);

            if (Tiles)
            {
                for (var i = 0; i < Data.Width; i++)
                {
                    for (var j = 0; j < Data.Height; j++)
                    {
                        int _x = i + x, _y = j + y;
                        if (!InMapBoundaries(_x, _y)) { continue; }
                        Main.tile[_x, _y] = Data.Tiles[i, j];
                        Main.tile[_x, _y].skipLiquid(true);
                    }
                }
            }

            ClearObjects(x, y, x + Data.Width, y + Data.Height);

            foreach (var sign in Data.Signs)
			{
				var id = Sign.ReadSign(sign.X + x, sign.Y + y);
                if ((id == -1) || !InMapBoundaries(sign.X, sign.Y))
                { continue; }
				Sign.TextSign(id, sign.Text);
			}

			foreach (var itemFrame in Data.ItemFrames)
			{
				var id = TEItemFrame.Place(itemFrame.X + x, itemFrame.Y + y);
				if (id == -1) { continue; }

                var frame = (TEItemFrame) TileEntity.ByID[id];
                if (!InMapBoundaries(frame.Position.X, frame.Position.Y))
                { continue; }
                frame.item = new Item();
				frame.item.netDefaults(itemFrame.Item.NetId);
				frame.item.stack = itemFrame.Item.Stack;
				frame.item.prefix = itemFrame.Item.PrefixId;
			}

			foreach (var chest in Data.Chests)
			{
				int chestX = chest.X + x, chestY = chest.Y + y;

				int id;
				if ((id = Chest.FindChest(chestX, chestY)) == -1 &&
				    (id = Chest.CreateChest(chestX, chestY)) == -1)
				{ continue; }
                Chest _chest = Main.chest[id];
                if (!InMapBoundaries(chest.X, chest.Y)) { continue; }

                for (var index = 0; index < chest.Items.Length; index++)
				{
					var netItem = chest.Items[index];
					var item = new Item();
					item.netDefaults(netItem.NetId);
					item.stack = netItem.Stack;
					item.prefix = netItem.PrefixId;
					Main.chest[id].item[index] = item;
				}
            }

            foreach (var logicSensor in Data.LogicSensors)
            {
                var id = TELogicSensor.Place(logicSensor.X + x, logicSensor.Y + y);
                if (id == -1) { continue; }
                var sensor = (TELogicSensor)TileEntity.ByID[id];
                if (!InMapBoundaries(sensor.Position.X, sensor.Position.Y))
                { continue; }
                sensor.logicCheck = logicSensor.Type;
            }

            foreach (var trainingDummy in Data.TrainingDummies)
            {
                var id = TETrainingDummy.Place(trainingDummy.X + x, trainingDummy.Y + y);
                if (id == -1) { continue; }
                var dummy = (TETrainingDummy)TileEntity.ByID[id];
                if (!InMapBoundaries(dummy.Position.X, dummy.Position.Y))
                { continue; }
                dummy.npc = -1;
            }

            ResetSection(x, y, x + Data.Width, y + Data.Height);
		}

		public static void PrepareUndo(int x, int y, int x2, int y2, TSPlayer plr)
		{
            if (WorldEdit.Config.DisableUndoSystemForUnrealPlayers && !plr.RealPlayer)
                return;

			if (WorldEdit.Database.GetSqlType() == SqlType.Mysql)
				WorldEdit.Database.Query("INSERT IGNORE INTO WorldEdit VALUES (@0, -1, -1)", plr.Account.ID);
			else
				WorldEdit.Database.Query("INSERT OR IGNORE INTO WorldEdit VALUES (@0, 0, 0)", plr.Account.ID);
			WorldEdit.Database.Query("UPDATE WorldEdit SET RedoLevel = -1 WHERE Account = @0", plr.Account.ID);
			WorldEdit.Database.Query("UPDATE WorldEdit SET UndoLevel = UndoLevel + 1 WHERE Account = @0", plr.Account.ID);

			int undoLevel = 0;
			using (var reader = WorldEdit.Database.QueryReader("SELECT UndoLevel FROM WorldEdit WHERE Account = @0", plr.Account.ID))
			{
				if (reader.Read())
					undoLevel = reader.Get<int>("UndoLevel");
			}

			string path = Path.Combine("worldedit", string.Format("undo-{0}-{1}.dat", plr.Account.ID, undoLevel));
			SaveWorldSection(x, y, x2, y2, path);

			foreach (string fileName in Directory.EnumerateFiles("worldedit", string.Format("redo-{0}-*.dat", plr.Account.ID)))
				File.Delete(fileName);
			File.Delete(Path.Combine("worldedit", string.Format("undo-{0}-{1}.dat", plr.Account.ID, undoLevel - MAX_UNDOS)));
		}

		public static bool Redo(int accountID)
        {
            if (WorldEdit.Config.DisableUndoSystemForUnrealPlayers && accountID == 0)
                return false;

            int redoLevel = 0;
			int undoLevel = 0;
			using (var reader = WorldEdit.Database.QueryReader("SELECT RedoLevel, UndoLevel FROM WorldEdit WHERE Account = @0", accountID))
			{
				if (reader.Read())
				{
					redoLevel = reader.Get<int>("RedoLevel") - 1;
					undoLevel = reader.Get<int>("UndoLevel") + 1;
				}
				else
					return false;
			}

			if (redoLevel < -1)
				return false;

			string redoPath = Path.Combine("worldedit", string.Format("redo-{0}-{1}.dat", accountID, redoLevel + 1));
			WorldEdit.Database.Query("UPDATE WorldEdit SET RedoLevel = @0 WHERE Account = @1", redoLevel, accountID);

			if (!File.Exists(redoPath))
				return false;

			string undoPath = Path.Combine("worldedit", string.Format("undo-{0}-{1}.dat", accountID, undoLevel));
			WorldEdit.Database.Query("UPDATE WorldEdit SET UndoLevel = @0 WHERE Account = @1", undoLevel, accountID);

			using (var reader = new BinaryReader(new GZipStream(new FileStream(redoPath, FileMode.Open), CompressionMode.Decompress)))
			{
				int x = Math.Max(0, reader.ReadInt32());
				int y = Math.Max(0, reader.ReadInt32());
				int x2 = Math.Min(x + reader.ReadInt32() - 1, Main.maxTilesX - 1);
				int y2 = Math.Min(y + reader.ReadInt32() - 1, Main.maxTilesY - 1);
				SaveWorldSection(x, y, x2, y2, undoPath);
			}
			LoadWorldSection(redoPath);
			File.Delete(redoPath);
			return true;
		}

		public static void ResetSection(int x, int y, int x2, int y2)
		{
			int lowX = Netplay.GetSectionX(x);
			int highX = Netplay.GetSectionX(x2);
			int lowY = Netplay.GetSectionY(y);
			int highY = Netplay.GetSectionY(y2);
			foreach (RemoteClient sock in Netplay.Clients.Where(s => s.IsActive))
			{
                int w = sock.TileSections.GetLength(0), h = sock.TileSections.GetLength(1);
                for (int i = lowX; i <= highX; i++)
				{
                    for (int j = lowY; j <= highY; j++)
                    {
                        if (i < 0 || j < 0 || i >= w || j >= h) { continue; }
                        sock.TileSections[i, j] = false;
                    }
				}
			}
		}

		public static void SaveWorldSection(int x, int y, int x2, int y2, string path)
		{
			using (var writer =
				new BinaryWriter(
					new BufferedStream(
						new GZipStream(File.Open(path, FileMode.Create), CompressionMode.Compress), BUFFER_SIZE)))
			{
				var data = SaveWorldSection(x, y, x2, y2);

				data.Write(writer);
			}
		}

		public static void Write(this BinaryWriter writer, ITile tile)
		{
			writer.Write(tile.sTileHeader);
			writer.Write(tile.bTileHeader);
			writer.Write(tile.bTileHeader2);

			if (tile.active())
			{
				writer.Write(tile.type);
				if (Main.tileFrameImportant[tile.type])
				{
					writer.Write(tile.frameX);
					writer.Write(tile.frameY);
				}
			}
			writer.Write(tile.wall);
			writer.Write(tile.liquid);
		}

		public static WorldSectionData SaveWorldSection(int x, int y, int x2, int y2)
		{
			var width = x2 - x + 1;
			var height = y2 - y + 1;

			var data = new WorldSectionData(width, height)
			{
				X = x,
				Y = y,
				Chests = new List<WorldSectionData.ChestData>(),
				Signs = new List<WorldSectionData.SignData>(),
				ItemFrames = new List<WorldSectionData.ItemFrameData>(),
                LogicSensors = new List<WorldSectionData.LogicSensorData>(),
                TrainingDummies = new List<WorldSectionData.TrainingDummyData>()
            };

			for (var i = x; i <= x2; i++)
			{
				for (var j = y; j <= y2; j++)
				{
					data.ProcessTile(Main.tile[i, j], i - x, j - y);
				}
			}

			return data;
		}

		public static bool Undo(int accountID)
        {
            if (WorldEdit.Config.DisableUndoSystemForUnrealPlayers && accountID == 0)
                return false;

            int redoLevel, undoLevel;
			using (var reader = WorldEdit.Database.QueryReader("SELECT RedoLevel, UndoLevel FROM WorldEdit WHERE Account = @0", accountID))
			{
				if (reader.Read())
				{
					redoLevel = reader.Get<int>("RedoLevel") + 1;
					undoLevel = reader.Get<int>("UndoLevel") - 1;
				}
				else
					return false;
			}

			if (undoLevel < -1)
				return false;

			string undoPath = Path.Combine("worldedit", string.Format("undo-{0}-{1}.dat", accountID, undoLevel + 1));
			WorldEdit.Database.Query("UPDATE WorldEdit SET UndoLevel = @0 WHERE Account = @1", undoLevel, accountID);

			if (!File.Exists(undoPath))
				return false;

			string redoPath = Path.Combine("worldedit", string.Format("redo-{0}-{1}.dat", accountID, redoLevel));
			WorldEdit.Database.Query("UPDATE WorldEdit SET RedoLevel = @0 WHERE Account = @1", redoLevel, accountID);

			using (var reader = new BinaryReader(new GZipStream(new FileStream(undoPath, FileMode.Open), CompressionMode.Decompress)))
			{
				int x = Math.Max(0, reader.ReadInt32());
				int y = Math.Max(0, reader.ReadInt32());
				int x2 = Math.Min(x + reader.ReadInt32() - 1, Main.maxTilesX - 1);
				int y2 = Math.Min(y + reader.ReadInt32() - 1, Main.maxTilesY - 1);
				SaveWorldSection(x, y, x2, y2, redoPath);
			}
			LoadWorldSection(undoPath);
			File.Delete(undoPath);
			return true;
		}

        public static bool CanSet(bool Tile, ITile tile, int type,
            Selection selection, Expressions.Expression expression,
            MagicWand magicWand, int x, int y, TSPlayer player) =>
            Tile
                ? ((((type >= 0) && (!tile.active() || (tile.type != type)))
                 || ((type == -1) && tile.active())
                 || ((type == -2) && ((tile.liquid == 0) || (tile.liquidType() != 1)))
                 || ((type == -3) && ((tile.liquid == 0) || (tile.liquidType() != 2)))
                 || ((type == -4) && ((tile.liquid == 0) || (tile.liquidType() != 0))))
                 && selection(x, y, player) && expression.Evaluate(tile)
                 && magicWand.InSelection(x, y))
                : ((tile.wall != type) && selection(x, y, player)
                 && expression.Evaluate(tile) && magicWand.InSelection(x, y));

        public static WEPoint[] CreateLine(int x1, int y1, int x2, int y2)
        {
            List<WEPoint> points = new List<WEPoint>()
            { new WEPoint((short)x1, (short)y1) };

            int diffX = x2 - x1, diffY = y2 - y1;
            int signX = diffX > 0 ? 1 : diffX < 0 ? -1 : 0;
            int signY = diffY > 0 ? 1 : diffY < 0 ? -1 : 0;
            if (diffX < 0) { diffX = -diffX; }
            if (diffY < 0) { diffY = -diffY; }

            int pdX, pdY, es, el;
            if (diffX > diffY)
            {
                pdX = signX;
                pdY = 0;
                es = diffY;
                el = diffX;
            }
            else
            {
                pdX = 0;
                pdY = signY;
                es = diffX;
                el = diffY;
            }

            int x = x1, y = y1, error = el / 2, t = 0;

            while (t < el)
            {
                error -= es;
                if (error < 0)
                {
                    error += el;
                    x += signX;
                    y += signY;
                }
                else
                {
                    x += pdX;
                    y += pdY;
                }
                t++;
                points.Add(new WEPoint((short)x, (short)y));
            }

            return points.ToArray();
        }

        public static bool InEllipse(int x1, int y1, int x2, int y2, int x, int y)
        {
            Vector2 center = new Vector2((float)(x2 - x1) / 2, (float)(y2 - y1) / 2);
            float rMax = Math.Max(center.X, center.Y), rMin = Math.Min(center.X, center.Y);
            if (center.Y > center.X)
            {
                float temp = rMax;
                rMax = rMin;
                rMin = temp;
            }
            return InEllipse(x1, y1, center.X, center.Y, rMax, rMin, x, y);
        }
        private static bool InEllipse(int x1, int y1, float cX,
            float cY, float rMax, float rMin, int x, int y) =>
            Math.Pow(x - cX - x1, 2) / Math.Pow(rMax, 2)
            + Math.Pow(y - cY - y1, 2) / Math.Pow(rMin, 2) <= 1;

        public static WEPoint[] CreateEllipseOutline(int x1, int y1, int x2, int y2)
        {
            Vector2 center = new Vector2((float)(x2 - x1) / 2, (float)(y2 - y1) / 2);
            float rMax = Math.Max(center.X, center.Y), rMin = Math.Min(center.X, center.Y);
            if (center.Y > center.X)
            {
                float temp = rMax;
                rMax = rMin;
                rMin = temp;
            }

            List<WEPoint> points = new List<WEPoint>();
            for (int i = x1; i <= (x2 - ((x2 - x1) / 2)); i++)
            {
                for (int j = y1; j <= (y2 - ((y2 - y1) / 2)); j++)
                {
                    if (InEllipse(x1, y1, center.X, center.Y, rMax, rMin, i, j))
                    {
                        if (points.Count > 0)
                        {
                            WEPoint point = points.Last();
                            int e = j;
                            while (point.Y - e >= 1)
                            { addPoint(points, x1, y1, x2, y2, i, e++); }
                        }
                        else
                        {
                            int a = y1 + ((y2 - y1) / 2) - j;
                            if (a > 0)
                            {
                                int e = j;
                                while (a-- >= 0)
                                { addPoint(points, x1, y1, x2, y2, i, e++); }
                            }
                        }
                        addPoint(points, x1, y1, x2, y2, i, j);
                        break;
                    }
                }
            }

            return points.ToArray();
        }
        private static void addPoint(List<WEPoint> points,
            int x1, int y1, int x2, int y2, int i, int j)
        {
            points.Add(new WEPoint((short)(x2 - i + x1), (short)j));
            points.Add(new WEPoint((short)i, (short)(y2 - j + y1)));
            points.Add(new WEPoint((short)(x2 - i + x1), (short)(y2 - j + y1)));
            points.Add(new WEPoint((short)i, (short)j));
        }

        public static WEPoint[,] CreateStatueText(string Text, int Width, int Height)
        {
            WEPoint[,] text = new WEPoint[Width, Height];
            if (string.IsNullOrWhiteSpace(Text)) { return text; }
            List<Tuple<WEPoint[,], int>> rows = new List<Tuple<WEPoint[,], int>>();
            string[] sRows = Text.ToLower().Replace("\\n", "\n").Split('\n');
            int height = 0;
            for (int i = 0; i < sRows.Length; i++)
            {
                Tuple<WEPoint[,], int> row = CreateStatueRow(sRows[i], Width, i == 0);
                if ((height += (row.Item1.GetLength(1) + row.Item2)) > Height) { break; }
                rows.Add(row);
            }

            int y = 0;
            foreach (Tuple<WEPoint[,], int> row in rows)
            {
                y += row.Item2;
                int w = row.Item1.GetLength(0), h = row.Item1.GetLength(1);
                for (int i = 0; i < w; i++)
                {
                    for (int j = 0; j < h; j++)
                    {
                        if (j + y > Height) { break; }
                        text[i, j + y] = row.Item1[i, j];
                    }
                }
                y += h;
            }
            return text;            
        }

        private static Tuple<WEPoint[,], int> CreateStatueRow(string Row, int Width, bool FirstRow)
        {
            Tuple<string, int, int, int> settings = RowSettings(Row, FirstRow);
            WEPoint[,] text = new WEPoint[Width, settings.Item4];
            List<char> letters = settings.Item1.ToCharArray().ToList();

            int diff = (int)Math.Ceiling((letters.Count * 2 - Width) / 2d), x = 0;
            if (diff > 0) { letters.RemoveRange(letters.Count - diff, diff); }

            if (settings.Item2 == 1 && letters.Count * 2 <= Width)
            { x = ((Width - (letters.Count * 2)) / 2); }
            else if (settings.Item2 == 2 && letters.Count * 2 <= Width)
            { x = (Width - (letters.Count * 2)); }

            for (int k = 0; k < letters.Count; k++)
            {
                WEPoint[,] letter = CreateStatueLetter(letters[k]);
                for (int i = 0; i < 2; i++)
                {
                    if (i + x > Width) { break; }
                    for (int j = 0; j < settings.Item4; j++)
                    { text[x, j] = letter[i, j]; }
                    x++;
                }
            }
            return new Tuple<WEPoint[,], int>(text, settings.Item3);
        }

        private static Tuple<string, int, int, int> RowSettings(string Row, bool FirstRow)
        {
            int style = 0, spacing = FirstRow ? 0 : 1, height = 3;
            while (Row.StartsWith("\\") && Row.Length > 1)
            {
                switch (char.ToLower(Row[1]))
                {
                    case 'l':
                        {
                            style = 0;
                            Row = Row.Substring(2);
                            break;
                        }
                    case 'm':
                        {
                            style = 1;
                            Row = Row.Substring(2);
                            break;
                        }
                    case 'r':
                        {
                            style = 2;
                            Row = Row.Substring(2);
                            break;
                        }
                    case 'c':
                        {
                            height = 2;
                            Row = Row.Substring(2);
                            break;
                        }
                    case 's':
                        {
                            Row = Row.Substring(2);
                            string num = "";
                            int index = 0;
                            while (Row.Length > index + 1)
                            {
                                if (char.IsDigit(Row[index]))
                                { num += Row[index++]; }
                                else { break; }
                            }
                            Row = Row.Substring(index);
                            if (!int.TryParse(num, out spacing)
                                || spacing < 0)
                            { spacing = FirstRow ? 0 : 1; }
                            break;
                        }
                }
            }
            return new Tuple<string, int, int, int>(Row, style, spacing, height);
        }

        private static WEPoint[,] CreateStatueLetter(char Letter)
        {
            WEPoint[,] letter = new WEPoint[2, 3];
            short leftTop, a = 0;
            if ((Letter > 47) && (Letter < 58))
            { leftTop = (short)((Letter - 48) * 36); }
            else if ((Letter > 96) && (Letter < 123))
            { leftTop = (short)((Letter - 87) * 36); }
            else { return letter; }
            
            for (short i = leftTop; i <= (leftTop + 18); i += 18)
            {
                int b = 0;
                for (short j = 0; j <= 36; j += 18)
                { letter[a, b++] = new WEPoint(i, j); }
                a++;
            }
            return letter;
        }
    }
}