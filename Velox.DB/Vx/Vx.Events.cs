﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Velox.DB
{
    public static partial class Vx
    {
        public interface IEvents<T>
        {
            event EventHandler<ObjectWithCancelEventArgs<T>> ObjectCreating;
            event EventHandler<ObjectEventArgs<T>> ObjectCreated;
            event EventHandler<ObjectWithCancelEventArgs<T>> ObjectSaving;
            event EventHandler<ObjectEventArgs<T>> ObjectSaved;
            event EventHandler<ObjectWithCancelEventArgs<T>> ObjectDeleting;
            event EventHandler<ObjectEventArgs<T>> ObjectDeleted;
        }

        public class ObjectEventArgs<T> : EventArgs
        {
            public T Value { get; set; }

            public ObjectEventArgs()
            {

            }

            public ObjectEventArgs(T value)
            {
                Value = value;
            }
        }

        public class ObjectWithCancelEventArgs<T> : ObjectEventArgs<T>
        {
            public bool Cancel { get; set; }

            public ObjectWithCancelEventArgs()
            {

            }

            public ObjectWithCancelEventArgs(T value)
            {
                Value = value;
            }
        }

    }
}
