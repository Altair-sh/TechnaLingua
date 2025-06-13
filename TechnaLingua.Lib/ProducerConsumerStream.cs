using System;
using System.IO;
using System.Threading.Tasks;

namespace TechnaLingua;

public class ProducerConsumerStream : IDisposable, IAsyncDisposable
{
    private readonly MemoryStream _innerStream;
    private WriteStream _writeStream;

    public ProducerConsumerStream() : this(new MemoryStream())
    {
    }
    
    public ProducerConsumerStream(MemoryStream innerStream)
    {
        _innerStream = innerStream;
        _writeStream =  new WriteStream(this);
    }

    public ReadStream CreateReadStream() => new ReadStream(this);
    
    public WriteStream GetWriteStream() => _writeStream;
    
    
    public void Dispose()
    {
        _innerStream.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        await _innerStream.DisposeAsync();
    }
    
    
    public abstract class MemoryStreamWrapperBase : Stream
    {
        protected readonly ProducerConsumerStream _parent;
        protected long _wrapperPosition;

        protected MemoryStreamWrapperBase(ProducerConsumerStream _parent)
        {
            this._parent = _parent;
        }

        public override bool CanRead => true;

        public override bool CanSeek => true;

        public override bool CanWrite => true;
        
        public override long Position
        {
            get => _wrapperPosition;
            set => _wrapperPosition = value;
        }
        
        public override long Length
        {
            get { lock (_parent._innerStream) { return _parent._innerStream.Length; } }
        }
        
        public override void Flush()
        {
            lock (_parent._innerStream) { _parent._innerStream.Flush(); }
        }

        public override void SetLength(long value) => throw new NotSupportedException();
        
        public override long Seek(long offset, SeekOrigin origin)
        {
            lock (_parent._innerStream)
            {
                switch (origin)
                {
                    case SeekOrigin.Begin:
                        _wrapperPosition = offset;
                        break;
                    case SeekOrigin.Current:
                        _wrapperPosition += offset;
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(origin), origin, null);
                }
                return _wrapperPosition;
            }
        }
    }

    public class ReadStream : MemoryStreamWrapperBase
    {
        internal ReadStream(ProducerConsumerStream parent) : base(parent)
        {
        }
        
        public override int Read(byte[] buffer, int offset, int count)
        {
            lock (_parent._innerStream)
            {
                _parent._innerStream.Position = _wrapperPosition;
                int red = _parent._innerStream.Read(buffer, offset, count);
                _wrapperPosition = _parent._innerStream.Position;
                return red;
            }
        }
        
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
    
    public class WriteStream : MemoryStreamWrapperBase
    {
        internal WriteStream(ProducerConsumerStream parent) : base(parent)
        {
        }

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        
        public override void Write(byte[] buffer, int offset, int count)
        {
            lock (_parent._innerStream)
            {
                _parent._innerStream.Position = _wrapperPosition;
                _parent._innerStream.Write(buffer, offset, count);
                _wrapperPosition = _parent._innerStream.Position;
            }
        }
    }
}