using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace ChengMuConsole
{
    public enum IconKind { Download, Database, Restore, Folder, Info, Bulb, Calendar, User, Chat, Search, Gear, Copy, Key }

    // =============================================================================
    //  線條圖示（家族 2026-10-19 方案一設計稿那一組，從 SE-Deploy 的 UiIcons.cs 複製）
    //
    //  座標是 24×24 的格子，畫的時候縮放進指定的方框。自己畫、不用圖示字型：業主的電腦不一定有 Segoe Fluent Icons。
    //  Calendar／User／Chat／Search／Gear／Copy／Key 是這支工具新加的，筆法照原本那組（圓角線頭、同樣粗細）。
    // =============================================================================
    public static class UiIcons
    {
        public static void Draw(Graphics g, IconKind kind, RectangleF box, Color color, float width)
        {
            var k = Math.Min(box.Width, box.Height) / 24f;
            float ox = box.X + (box.Width - 24 * k) / 2, oy = box.Y + (box.Height - 24 * k) / 2;
            Func<float, float, PointF> P = (x, y) => new PointF(ox + x * k, oy + y * k);
            Func<float, float, float, float, RectangleF> R = (x, y, w, h) => new RectangleF(ox + x * k, oy + y * k, w * k, h * k);
            var mode = g.SmoothingMode;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var pen = new Pen(color, width) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
            {
                switch (kind)
                {
                    case IconKind.Download:
                        g.DrawLine(pen, P(12, 4), P(12, 15));
                        g.DrawLines(pen, new[] { P(7.5f, 10.5f), P(12, 15), P(16.5f, 10.5f) });
                        g.DrawLines(pen, new[] { P(4, 17), P(4, 20), P(20, 20), P(20, 17) });
                        break;
                    case IconKind.Database:
                        g.DrawEllipse(pen, R(4, 3, 16, 6));
                        g.DrawLine(pen, P(4, 6), P(4, 18));
                        g.DrawLine(pen, P(20, 6), P(20, 18));
                        g.DrawArc(pen, R(4, 9, 16, 6), 0, 180);
                        g.DrawArc(pen, R(4, 15, 16, 6), 0, 180);
                        break;
                    case IconKind.Restore:
                        // 兩段圓弧各接一個箭頭（設計稿的 refresh 圖示）
                        g.DrawArc(pen, R(4, 4, 16, 16), 0, 142);
                        g.DrawArc(pen, R(4, 4, 16, 16), 180, 142);
                        g.DrawLines(pen, new[] { P(18.5f, 3), P(18.5f, 7.2f), P(14.3f, 7.2f) });
                        g.DrawLines(pen, new[] { P(5.5f, 21), P(5.5f, 16.8f), P(9.7f, 16.8f) });
                        break;
                    case IconKind.Folder:
                        g.DrawPolygon(pen, new[] { P(3, 6), P(9, 6), P(11, 8.5f), P(21, 8.5f), P(21, 19), P(3, 19) });
                        break;
                    case IconKind.Info:
                        g.DrawEllipse(pen, R(3, 3, 18, 18));
                        g.DrawLine(pen, P(12, 11), P(12, 16.5f));
                        g.DrawLine(pen, P(12, 7.6f), P(12, 7.7f));
                        break;
                    case IconKind.Bulb:
                        using (var path = new GraphicsPath())
                        {
                            path.AddArc(R(6, 3, 12, 12), 135, 270);
                            path.AddBezier(P(16.2f, 13.2f), P(15.4f, 14), P(14.5f, 14.8f), P(14.5f, 16));
                            path.AddLine(P(14.5f, 16), P(9.5f, 16));
                            path.AddBezier(P(9.5f, 16), P(9.5f, 14.8f), P(8.6f, 14), P(7.8f, 13.2f));
                            path.CloseFigure();
                            g.DrawPath(pen, path);
                        }
                        g.DrawLine(pen, P(9, 18), P(15, 18));
                        g.DrawLine(pen, P(10, 21), P(14, 21));
                        break;
                    case IconKind.Calendar:
                        g.DrawPolygon(pen, new[] { P(4, 6), P(20, 6), P(20, 20), P(4, 20) });
                        g.DrawLine(pen, P(4, 10), P(20, 10));
                        g.DrawLine(pen, P(8.5f, 3.5f), P(8.5f, 7.5f));
                        g.DrawLine(pen, P(15.5f, 3.5f), P(15.5f, 7.5f));
                        break;
                    case IconKind.User:
                        g.DrawEllipse(pen, R(8, 3.5f, 8, 8));
                        g.DrawArc(pen, R(4.5f, 13.5f, 15, 13), 180, 180);
                        break;
                    case IconKind.Chat:
                        g.DrawLines(pen, new[] { P(4, 5), P(20, 5), P(20, 16), P(11, 16), P(6.5f, 20), P(6.5f, 16), P(4, 16), P(4, 5) });
                        g.DrawLine(pen, P(8, 9), P(16, 9));
                        g.DrawLine(pen, P(8, 12.3f), P(13.5f, 12.3f));
                        break;
                    case IconKind.Search:
                        g.DrawEllipse(pen, R(4, 4, 12, 12));
                        g.DrawLine(pen, P(14.5f, 14.5f), P(20, 20));
                        break;
                    case IconKind.Gear:
                        // 八齒齒輪：每一齒內圈兩點、外圈兩點，中間一個圓孔
                        var gear = new PointF[32];
                        for (int i = 0; i < 8; i++)
                        {
                            var a = i * 45.0;
                            var angles = new[] { a - 19, a - 9, a + 9, a + 19 };
                            var radii = new[] { 6.6f, 9f, 9f, 6.6f };
                            for (int j = 0; j < 4; j++)
                            {
                                var t = angles[j] * Math.PI / 180;
                                gear[i * 4 + j] = P(12 + radii[j] * (float)Math.Cos(t), 12 + radii[j] * (float)Math.Sin(t));
                            }
                        }
                        g.DrawPolygon(pen, gear);
                        g.DrawEllipse(pen, R(9, 9, 6, 6));
                        break;
                    case IconKind.Copy:
                        g.DrawPolygon(pen, new[] { P(9, 8), P(19, 8), P(19, 20), P(9, 20) });
                        g.DrawLines(pen, new[] { P(15, 8), P(15, 4), P(5, 4), P(5, 16), P(9, 16) });
                        break;
                    case IconKind.Key:
                        g.DrawEllipse(pen, R(3.5f, 9, 7, 7));
                        g.DrawLines(pen, new[] { P(10.5f, 12.5f), P(20.5f, 12.5f), P(20.5f, 16) });
                        g.DrawLine(pen, P(16.5f, 12.5f), P(16.5f, 15.5f));
                        break;
                }
            }
            g.SmoothingMode = mode;
        }
    }
}
