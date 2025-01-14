﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Drawing;
using System.Diagnostics;

namespace Foreman
{
	public class LinkElement : BaseLinkElement
	{
		public ReadOnlyNodeLink DisplayedLink { get; private set; }
		public override ItemQualityPair Item { get { return DisplayedLink.Item; } protected set { } }

		public ItemTabElement SupplierTab { get; protected set; }
		public ItemTabElement ConsumerTab { get; protected set; }

		public LinkElement(ProductionGraphViewer graphViewer, ReadOnlyNodeLink displayedLink, BaseNodeElement supplierElement, BaseNodeElement consumerElement) : base(graphViewer)
		{
			if (supplierElement == null || consumerElement == null)
				Trace.Fail("Link element being created with one of the connected elements being null!");

			DisplayedLink = displayedLink;
			SupplierElement = supplierElement;
			ConsumerElement = consumerElement;
			SupplierTab = supplierElement.GetOutputLineItemTab(Item);
			ConsumerTab = consumerElement.GetInputLineItemTab(Item);

			if (SupplierTab == null || ConsumerTab == null)
				Trace.Fail(string.Format("Link element being created with one of the elements ({0}, {1}) not having the required item ({2})!", supplierElement, consumerElement, Item));

			LinkWidth = 3f;
			UpdateCurve();
		}

		protected override Tuple<Point,Point> GetCurveEndpoints()
		{
			return new Tuple<Point, Point>(iconOnlyDraw? SupplierElement.Location : SupplierTab.GetConnectionPoint(), iconOnlyDraw? ConsumerElement.Location : ConsumerTab.GetConnectionPoint());
		}
		protected override Tuple<NodeDirection, NodeDirection> GetEndpointDirections()
		{
			return new Tuple<NodeDirection, NodeDirection>(SupplierElement.DisplayedNode.NodeDirection, ConsumerElement.DisplayedNode.NodeDirection);
		}
	}
}
