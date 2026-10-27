using System.Drawing;
using System.Windows.Forms;

namespace PineTree.UserControls
{
    public partial class UcConfig : UserControl
    {
        private readonly Rectangle[] _cardBounds =
        {
            new Rectangle(32, 127, 306, 84),
            new Rectangle(356, 127, 305, 84),
            new Rectangle(680, 127, 306, 84)
        };
        private int _selectedCardIndex = 0;

        public UcConfig()
        {
            InitializeComponent();

            picBackground.Paint += PicBackground_Paint;
            picBackground.MouseClick += PicBackground_MouseClick;
            picBackground.MouseMove += PicBackground_MouseMove;
            picBackground.MouseLeave += delegate { picBackground.Cursor = Cursors.Default; };

            Label[] cardLabels = { lblCommonCode, lblProductFinishingRules, lblOther };
            for (int index = 0; index < cardLabels.Length; index++)
            {
                int cardIndex = index;
                cardLabels[index].Cursor = Cursors.Hand;
                cardLabels[index].MouseClick += delegate(object sender, MouseEventArgs e)
                {
                    if (e.Button == MouseButtons.Left)
                    {
                        SelectCard(cardIndex);
                    }
                };
            }
        }

        private void SelectCard(int cardIndex)
        {
            if (_selectedCardIndex == cardIndex)
            {
                return;
            }

            Rectangle previousCard = _cardBounds[_selectedCardIndex];
            _selectedCardIndex = cardIndex;
            ucCommonCode.Visible = _selectedCardIndex == 0;
            ucProcessRule.Visible = _selectedCardIndex == 1;
            picBackground.Invalidate(previousCard);
            picBackground.Invalidate(_cardBounds[_selectedCardIndex]);
        }

        private int GetCardIndex(Point location)
        {
            for (int index = 0; index < _cardBounds.Length; index++)
            {
                if (_cardBounds[index].Contains(location))
                {
                    return index;
                }
            }

            return -1;
        }

        private void PicBackground_MouseClick(object sender, MouseEventArgs e)
        {
            int cardIndex = GetCardIndex(e.Location);
            if (e.Button == MouseButtons.Left && cardIndex >= 0)
            {
                SelectCard(cardIndex);
            }
        }

        private void PicBackground_MouseMove(object sender, MouseEventArgs e)
        {
            picBackground.Cursor = GetCardIndex(e.Location) >= 0 ? Cursors.Hand : Cursors.Default;
        }

        private void PicBackground_Paint(object sender, PaintEventArgs e)
        {
            Rectangle card = _cardBounds[_selectedCardIndex];
            using (SolidBrush brush = new SolidBrush(Color.FromArgb(41, 199, 235)))
            {
                e.Graphics.FillRectangle(brush, card.Left + 7, card.Top + 11, 5, card.Height - 22);
            }
        }
    }
}
