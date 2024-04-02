namespace MultiplayerARPG
{
    public partial struct CharacterItem
    {
        public bool IsEmpty()
        {
            return Equals(Empty);
        }

        public bool IsEmptySlot()
        {
            return IsEmpty() || dataId == 0 || amount <= 0;
        }

        public bool NotEmptySlot()
        {
            return !IsEmptySlot();
        }
    }
}
