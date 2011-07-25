﻿#region License
/*
Copyright © Joan Charmant 2011.
joan.charmant@gmail.com 
 
This file is part of Kinovea.

Kinovea is free software: you can redistribute it and/or modify
it under the terms of the GNU General Public License version 2 
as published by the Free Software Foundation.

Kinovea is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU General Public License for more details.

You should have received a copy of the GNU General Public License
along with Kinovea. If not, see http://www.gnu.org/licenses/.
*/
#endregion
using System;
using System.Drawing;
using System.Windows.Forms;
using System.Xml;

using Kinovea.ScreenManager.Languages;

namespace Kinovea.ScreenManager
{
	/// <summary>
	/// Style element to represent a color used by the drawing.
	/// Editor: clickable area giving access to a color picker dialog.
	/// </summary>
	public class StyleElementColor : AbstractStyleElement
	{
		#region Properties
		public override object Value
		{
			get { return m_Color; }
			set 
			{ 
				m_Color = (value is Color) ? (Color)value : Color.Black;
				RaiseValueChanged();
			}
		}
		public override Bitmap Icon
		{
			get { return Properties.Drawings.editorcolor;}
		}
		public override string DisplayName
		{
			get { return ScreenManagerLang.Generic_ColorPicker;}
		}
		#endregion
		
		#region Members
		private Color m_Color;
		#endregion
		
		#region Constructor
		public StyleElementColor(Color _default)
		{
			m_Color = _default;
		}
		#endregion
		
		#region Public Methods
		public override Control GetEditor()
		{
			Control editor = new Control();
			editor.Click += new EventHandler(editor_Click);
			editor.Paint += new PaintEventHandler(editor_Paint);
			return editor;
		}
		public override AbstractStyleElement Clone()
		{
			AbstractStyleElement clone = new StyleElementColor(m_Color);
			clone.Bind(this);
			return clone;
		}
		public override void ReadXML(XmlReader _xmlReader)
		{
			throw new NotImplementedException();
		}
		public override void WriteXml(XmlWriter _xmlWriter)
		{
			throw new NotImplementedException();
		}
		#endregion
		
		#region Private Methods
		private void editor_Paint(object sender, PaintEventArgs e)
		{
			using(SolidBrush b = new SolidBrush(m_Color))
			{
				e.Graphics.FillRectangle(b, e.ClipRectangle);
				e.Graphics.DrawRectangle(Pens.LightGray, e.ClipRectangle.Left, e.ClipRectangle.Top, e.ClipRectangle.Width - 1, e.ClipRectangle.Height - 1);	
			}
		}
		private void editor_Click(object sender, EventArgs e)
		{
			FormColorPicker picker = new FormColorPicker();
			ScreenManagerKernel.LocateForm(picker);
			if(picker.ShowDialog() == DialogResult.OK)
			{
				m_Color = picker.PickedColor;
				RaiseValueChanged();
				((Control)sender).Invalidate();
			}
			picker.Dispose();
		}	
		#endregion
	}
}