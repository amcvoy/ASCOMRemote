﻿using System;

namespace ASCOM.Remote
{
    public class IntArray2DResponse : ImageArrayResponseBase
    {
        private int[,] intArray2D;

        private const int RANK = 2;
        private const SharedConstants.ImageArrayElementTypes TYPE = SharedConstants.ImageArrayElementTypes.Int;

        public IntArray2DResponse(int clientTransactionID, int transactionID, string method)
        {
            base.ServerTransactionID = transactionID;
            base.Method = method;
            base.ClientTransactionID = clientTransactionID;
        }

        public int[,] Value
        {
            get { return intArray2D; }
            set
            {
                intArray2D = value;
                base.Type = (int)TYPE;
                base.Rank = RANK;
            }
        }
    }
}
