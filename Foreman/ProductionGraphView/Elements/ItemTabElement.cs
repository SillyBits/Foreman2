﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Drawing;
using System.Windows.Forms;

namespace Foreman
{
	public class ItemTabElement : GraphElement
	{
		public static int TabWidth { get { return iconSize + border * 3; } } //I just use these two to get a decent aproximation as to how far to space new nodes when bulk-added
		public static int TabBorder { get { return border; } }

		public LinkType LinkType;
		public Item Item { get; private set; }

		private const int iconSize = 32;
		private const int border = 3;
		private int textHeight = 11;

		private static StringFormat bottomFormat = new StringFormat() { LineAlignment = StringAlignment.Far, Alignment = StringAlignment.Center };
		private static StringFormat topFormat = new StringFormat() { LineAlignment = StringAlignment.Near, Alignment = StringAlignment.Center };
		private static Pen regularBorderPen = new Pen(Color.Gray, 3);
		private static Pen oversuppliedBorderPen = new Pen(Color.DarkRed, 3);
		private static Brush textBrush = new SolidBrush(Color.Black);
		private static Brush fillBrush = new SolidBrush(Color.White);

		private static Font textFont = new Font(FontFamily.GenericSansSerif, 6);

		private Pen borderPen;
		private string text = "";

		public ItemTabElement(Item item, LinkType type, ProductionGraphViewer graphViewer, BaseNodeElement node) //item tab is always to be owned by a node
			: base(graphViewer, node)
		{
			RightClickMenu = new ContextMenu();

			this.Item = item;
			this.LinkType = type;

			borderPen = regularBorderPen;
			int textHeight = (int)base.graphViewer.CreateGraphics().MeasureString("a", textFont).Height;
			Width = TabWidth;
			Height = iconSize + textHeight + border + 3;
			X = 0; Y = 0;
		}

		public Point GetConnectionPoint() //in graph coordinates
		{
			if (LinkType == LinkType.Input)
				return LocalToGraph(new Point(0, Height / 2));
			else //if(LinkType == LinkType.Output)
				return LocalToGraph(new Point(0, -Height / 2));
		}

		public void UpdateValues(float consumeRate, float suppliedRate, bool isOversupplied)
		{
			borderPen = regularBorderPen;
			text = FloatToString(consumeRate);

			if (isOversupplied)
			{
				borderPen = oversuppliedBorderPen;
				text += "\n" + FloatToString(suppliedRate);
			}

			int textHeight = (int)graphViewer.CreateGraphics().MeasureString(text, textFont).Height;
			Height = iconSize + textHeight + border + 3;
		}

		private string FloatToString(float value)
		{
			string str;
			if (value >= 100000)
				str = value.ToString("0.00e0");
			else if (value >= 10000)
				str = value.ToString("0");
			else if (value >= 100)
				str = value.ToString("0.#");
			else if (value >= 10)
				str = value.ToString("0.##");
			else if (value >= 0.1)
				str = value.ToString("0.###");
			else if (value != 0)
				str = value.ToString("0.######");
			else
				str = "0";
			return str;
		}

		protected override void Draw(Graphics graphics)
		{
			Point trans = LocalToGraph(new Point(0, 0));

			GraphicsStuff.FillRoundRect(trans.X - (Bounds.Width / 2), trans.Y - (Bounds.Height / 2), Bounds.Width, Bounds.Height, border, graphics, fillBrush);
			GraphicsStuff.DrawRoundRect(trans.X - (Bounds.Width / 2), trans.Y - (Bounds.Height / 2), Bounds.Width, Bounds.Height, border, graphics, borderPen);

			if (LinkType == LinkType.Output)
			{
				graphics.DrawString(text, textFont, textBrush, new PointF(trans.X, trans.Y + ((textHeight + border - Bounds.Height - 12) / 2)), topFormat);
				graphics.DrawImage(Item.Icon ?? DataCache.UnknownIcon, trans.X - (Bounds.Width / 2) + (int)(border * 1.5), trans.Y + (Bounds.Height / 2) - (int)(border * 1.5) - iconSize, iconSize, iconSize);
			}
			else
			{
				graphics.DrawString(text, textFont, textBrush, new PointF(trans.X, trans.Y - ((textHeight + border - Bounds.Height - 12) / 2)), bottomFormat);
				graphics.DrawImage(Item.Icon ?? DataCache.UnknownIcon, trans.X - (Bounds.Width / 2) + (int)(border * 1.5), trans.Y - (Bounds.Height / 2) + (int)(border * 1.5), iconSize, iconSize);
			}
		}

		public override List<TooltipInfo> GetToolTips(Point graph_point)
		{
			List<TooltipInfo> toolTips = new List<TooltipInfo>();
			TooltipInfo tti = new TooltipInfo();
			BaseNodeElement parentNode = (BaseNodeElement)myParent;

			if (parentNode.DisplayedNode is RecipeNode rNode)
				tti.Text = rNode.BaseRecipe.GetIngredientFriendlyName(Item);
			else if (!Item.IsTemperatureDependent)
				tti.Text = Item.FriendlyName;
			else
			{
				fRange tempRange = LinkChecker.GetTemperatureRange(Item, parentNode.DisplayedNode, (LinkType == LinkType.Input) ? LinkType.Output : LinkType.Input); //input type tab means output of connection link and vice versa
				if (tempRange.Ignore && parentNode.DisplayedNode is PassthroughNode)
					tempRange = LinkChecker.GetTemperatureRange(Item, parentNode.DisplayedNode, LinkType); //if there was no temp range on this side of this throughput node, try to just copy the other side
				tti.Text = Item.GetTemperatureRangeFriendlyName(tempRange);
			}

			tti.Direction = (LinkType == LinkType.Input) ? Direction.Up : Direction.Down;
			tti.ScreenLocation = graphViewer.GraphToScreen(GetConnectionPoint());
			toolTips.Add(tti);

			TooltipInfo helpToolTipInfo = new TooltipInfo();
			helpToolTipInfo.Text = "Drag to create a new connection.\nRight click for options.";
			helpToolTipInfo.Direction = Direction.None;
			helpToolTipInfo.ScreenLocation = new Point(10, 10);
			toolTips.Add(helpToolTipInfo);

			return toolTips;
		}

		public override void MouseUp(Point graph_point, MouseButtons button, bool wasDragged)
		{
			if (button == MouseButtons.Right)
			{
				List<NodeLink> connections = new List<NodeLink>();
				if (LinkType == LinkType.Input)
					connections.AddRange(((BaseNodeElement)myParent).DisplayedNode.InputLinks.Where(l => l.Item == Item));
				else //if (LinkType == LinkType.Output)
					connections.AddRange(((BaseNodeElement)myParent).DisplayedNode.OutputLinks.Where(l => l.Item == Item));


				RightClickMenu.MenuItems.Clear();
				RightClickMenu.MenuItems.Add(new MenuItem("Delete connections",
					new EventHandler((o, e) =>
					{
						foreach (NodeLink link in connections)
							graphViewer.Graph.DeleteLink(link);
						graphViewer.Graph.UpdateNodeValues();
					}))
				{ Enabled = connections.Count > 0 });

				RightClickMenu.Show(graphViewer, graphViewer.GraphToScreen(graph_point));
			}
		}
	}
}