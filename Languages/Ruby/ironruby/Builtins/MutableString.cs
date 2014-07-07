/* ****************************************************************************
 *
 * Copyright (c) Microsoft Corporation. 
 *
 * This source code is subject to terms and conditions of the Microsoft Public License. A 
 * copy of the license can be found in the License.html file at the root of this distribution. If 
 * you cannot locate the  Microsoft Public License, please send an email to 
 * ironruby@microsoft.com. By using this source code in any fashion, you are agreeing to be bound 
 * by the terms of the Microsoft Public License.
 *
 * You must not remove this notice, or any other, from this software.
 *
 *
 * ***************************************************************************/

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Linq;
using IronRuby.Compiler;
using IronRuby.Runtime;
using Microsoft.Scripting.Utils;
using System.Globalization;

namespace IronRuby.Builtins {

    // Doesn't implement IRubyObject since that would require to hold on a RubyClass object and flow it into each factory.
    // We don't want to do so since it would make libraries complex and frozen per-appdomain singletons impossible.
    // It would also consume more memory while the string subclassing is not a common scenario.
    // To allow inheriting from String in Ruby, we need a subclass that implements IRubyObject.
    // We could genrate one the first time a String is subclassed. Having it defined explicitly (MutableString.Subclass) however
    // saves that code gen and also makes it simpler to detect whether or not we need to create a subclass of a string fast. 
    // That's a common operation String methods do.
    [Serializable]
    [DebuggerDisplay("{GetDebugValue()}", Type = "{GetDebugType()}")]
    public partial class MutableString : IEquatable<MutableString>, IComparable<MutableString>, IRubyObjectState, IDuplicable {
        private Content/*!*/ _content;
        private RubyEncoding/*!*/ _encoding;
        
        private uint _flags = AsciiUnknownFlag | HashUnknownFlag;
        private int _hashCode;

        // true if frozen:
        private const uint IsFrozenFlag = 1;

        // set every time a change occurs (visible externally):
        private const uint HasChangedFlag = 2;

        // set every time a change occurs, used to track CharArrayContent._immutableSnapshot validity:
        private const uint HasChangedCharArrayToStringFlag = 4;

        private const uint HasChangedFlags = HasChangedFlag | HasChangedCharArrayToStringFlag;

        // true if all bytes/characters are < x80
        private const uint IsAsciiFlag = 8;
        
        // true if IsAsciiFlag is not up-to-date:
        private const uint AsciiUnknownFlag = 16;

        // true if _hashCode is not up-to-date:
        private const uint HashUnknownFlag = 32;

        // true if the string encoding is BinaryEncoding:
        private const uint IsBinaryEncodedFlag = 64;

        // true if tainted:
        private const uint IsTaintedFlag = 128;

        // The instance is frozen so that it can be shared, but it should not be used in places where
        // it will be accessible from user code as the user code could try to mutate it.
        public static readonly MutableString FrozenEmpty = CreateEmpty().Freeze();

        #region Constructors

        private void SetContent(Content/*!*/ content) {
            Assert.NotNull(content);
            content.SetOwner(this);
            _content = content;
        }

        private void SetEncoding(RubyEncoding/*!*/ encoding) {
            _encoding = encoding;
            if (encoding == RubyEncoding.Binary) {
                _flags |= IsBinaryEncodedFlag;
            } else {
                _flags &= ~IsBinaryEncodedFlag;
            }
        }

        private MutableString(Content/*!*/ content, RubyEncoding/*!*/ encoding) {
            Assert.NotNull(content, encoding);
            SetEncoding(encoding);
            SetContent(content);
        }

        // creates a copy including the taint flag:
        protected MutableString(MutableString/*!*/ str) 
            : this(str._content.Clone(), str._encoding) {
            IsTainted = str.IsTainted;
        }

        // mutable (doesn't make a copy of the array):
        private MutableString(char[]/*!*/ chars, RubyEncoding/*!*/ encoding)
            : this(new CharArrayContent(chars, null), encoding) {
        }

        // mutable (doesn't make a copy of the array):
        private MutableString(char[]/*!*/ chars, int count, RubyEncoding/*!*/ encoding)
            : this(new CharArrayContent(chars, count, null), encoding) {
        }

        // binary (doesn't make a copy of the array):
        private MutableString(byte[]/*!*/ bytes, RubyEncoding/*!*/ encoding)
            : this(encoding.IsKCoding ? new KBinaryContent(bytes, null) : new BinaryContent(bytes, null), encoding) {
        }

        // binary (doesn't make a copy of the array):
        // used by RubyBufferedStream:
        internal MutableString(byte[]/*!*/ bytes, int count, RubyEncoding/*!*/ encoding)
            : this(encoding.IsKCoding ? new KBinaryContent(bytes, count, null) : new BinaryContent(bytes, count, null), encoding) {
        }

        // immutable:
        private MutableString(string/*!*/ str, RubyEncoding/*!*/ encoding)
            : this(new StringContent(str, null), encoding) {
        }

        // mutable (visible for subclasses):
        protected MutableString(RubyEncoding/*!*/ encoding)
            : this(new CharArrayContent(Utils.EmptyChars, 0, null), encoding) {
        }

        // Ruby allocator
        public MutableString() 
            : this(String.Empty, RubyEncoding.Binary) {
        }

        #endregion

        #region Factories

        public static MutableString/*!*/ CreateMutable(RubyEncoding/*!*/ encoding) {
            return new MutableString(encoding);
        }

        public static MutableString/*!*/ CreateMutable(int capacity, RubyEncoding/*!*/ encoding) {
            ContractUtils.Requires(capacity >= 0, "Capacity must be greater or equal to zero.");
            ContractUtils.RequiresNotNull(encoding, "encoding");
            return new MutableString(new char[capacity], 0, encoding);
        }

        public static MutableString/*!*/ CreateMutable(string/*!*/ str, RubyEncoding/*!*/ encoding) {
            ContractUtils.RequiresNotNull(str, "str");
            ContractUtils.RequiresNotNull(encoding, "encoding");
            return new MutableString(str, encoding);
        }

        public static MutableString CreateAscii(string/*!*/ str) {
            ContractUtils.RequiresNotNull(str, "str");
            Debug.Assert(str.IsAscii());
            return Create(str, RubyEncoding.Binary);
        }

        public static MutableString/*!*/ Create(string/*!*/ str, RubyEncoding/*!*/ encoding) {
            ContractUtils.RequiresNotNull(str, "str");
            ContractUtils.RequiresNotNull(encoding, "encoding");
            return new MutableString(str, encoding);
        }

        public static MutableString/*!*/ CreateBinary() {
            return new MutableString(Utils.EmptyBytes, 0, RubyEncoding.Binary);
        }

        public static MutableString/*!*/ CreateBinary(RubyEncoding/*!*/ encoding) {
            return new MutableString(Utils.EmptyBytes, 0, encoding);
        }

        public static MutableString/*!*/ CreateBinary(int capacity) {
            return CreateBinary(capacity, RubyEncoding.Binary);
        }

        public static MutableString/*!*/ CreateBinary(int capacity, RubyEncoding/*!*/ encoding) {
            ContractUtils.Requires(capacity >= 0, "Capacity must be greater or equal to zero.");
            ContractUtils.RequiresNotNull(encoding, "encoding");
            return new MutableString(new byte[capacity], 0, encoding);
        }

        public static MutableString/*!*/ CreateBinary(byte[]/*!*/ bytes) {
            return CreateBinary(bytes, RubyEncoding.Binary);
        }

        public static MutableString/*!*/ CreateBinary(byte[]/*!*/ bytes, RubyEncoding/*!*/ encoding) {
            ContractUtils.RequiresNotNull(bytes, "bytes");
            ContractUtils.RequiresNotNull(encoding, "encoding");
            return new MutableString(ArrayUtils.Copy(bytes), encoding);
        }

        public static MutableString/*!*/ CreateBinary(List<byte>/*!*/ bytes, RubyEncoding/*!*/ encoding) {
            ContractUtils.RequiresNotNull(bytes, "bytes");
            ContractUtils.RequiresNotNull(encoding, "encoding");
            return new MutableString(bytes.ToArray(), encoding);
        }

        /// <summary>
        /// Creates an instance of MutableString with content and taint copied from a given string.
        /// </summary>
        public static MutableString/*!*/ Create(MutableString/*!*/ str) {
            ContractUtils.RequiresNotNull(str, "str");
            return new MutableString(str);
        }

        // used by RubyOps:
        internal static MutableString/*!*/ CreateInternal(MutableString str, RubyEncoding/*!*/ encoding) {
            if (str != null) {
                // "...#{str}..."
                return new MutableString(str);
            } else {
                // empty literal: "...#{nil}..."
                return CreateMutable(String.Empty, encoding);
            }
        }
        
        /// <summary>
        /// Creates a blank instance of self type with no flags set.
        /// Copies encoding from the current class.
        /// </summary>
        public virtual MutableString/*!*/ CreateInstance() {
            return new MutableString(_encoding);
        }

        public static MutableString/*!*/ CreateEmpty() {
            return MutableString.Create(String.Empty, RubyEncoding.Binary);
        }

        /// <summary>
        /// Creates a copy of this instance, including content and taint.
        /// Doesn't copy frozen state and instance variables. 
        /// Preserves the class of the String.
        /// </summary>
        public virtual MutableString/*!*/ Clone() {
            return new MutableString(this);
        }

        /// <summary>
        /// Creates an empty copy of this instance, taint and instance variables. 
        /// </summary>
        public MutableString/*!*/ Duplicate(RubyContext/*!*/ context, bool copySingletonMembers, MutableString/*!*/ result) {
            context.CopyInstanceData(this, result, copySingletonMembers);
            return result;
        }

        object IDuplicable.Duplicate(RubyContext/*!*/ context, bool copySingletonMembers) {
            return Duplicate(context, copySingletonMembers, CreateInstance());
        }

        public static MutableString[]/*!*/ MakeArray(ICollection<string>/*!*/ stringCollection, RubyEncoding/*!*/ encoding) {
            ContractUtils.RequiresNotNull(stringCollection, "stringCollection");
            ContractUtils.RequiresNotNull(encoding, "encoding");

            MutableString[] result = new MutableString[stringCollection.Count];
            int i = 0;
            foreach (var str in stringCollection) {
                result[i++] = MutableString.Create(str, encoding);
            }
            return result;
        }

        #endregion

        #region Versioning, Encoding, HashCode, and Flags

        /// <summary>
        /// Returns true if its character and byte representation is guaranteed to be the same per character or byte, i.e.
        /// if the string is known to contain ASCII characters only or if the string is binary-encoded.
        /// 
        /// Doesn't inspect the content of the string if the ASCII flag is not valid.
        /// </summary>
        public bool HasByteCharacters {
            get {
                var flags = _flags;
                return (flags & IsBinaryEncodedFlag) != 0
                    || (flags & (AsciiUnknownFlag | IsAsciiFlag)) == IsAsciiFlag; 
            }
        }

        private void MutateContent(uint setFlags) {
            uint flags = _flags;
            if ((flags & IsFrozenFlag) != 0) {
                throw RubyExceptions.CreateObjectFrozenError();
            }
            _flags = flags | setFlags;
        }

        private void Mutate() {
            MutateContent(HasChangedFlags | AsciiUnknownFlag | HashUnknownFlag);
        }

        /// <summary>
        /// Set or Insert a single character or byte.
        /// </summary>
        [Conditional("INLINED")]
        private void MutateOne(int charOrByte) {
            uint flags = _flags;
            if ((flags & IsFrozenFlag) != 0) {
                throw RubyExceptions.CreateObjectFrozenError();
            }
            _flags = flags | (charOrByte >= 0x80 ? HasChangedFlags | HashUnknownFlag | AsciiUnknownFlag : HasChangedFlags | HashUnknownFlag);
        }

        /// <summary>
        /// Operation preserves ascii-ness of the string.
        /// </summary>
        private void MutatePreserveAsciiness() {
            MutateContent(HasChangedFlags | HashUnknownFlag);
        }

        /// <summary>
        /// Operation removes characters or bytes.
        /// If the string was ascii before the operation it is ascii afterwards.
        /// </summary>
        private void MutateRemove() {
            MutateContent(
                (_flags & IsAsciiFlag) != 0 ? HasChangedFlags | HashUnknownFlag : HasChangedFlags | HashUnknownFlag | AsciiUnknownFlag
            );
        }

        /// <summary>
        /// Prepares the string for mutation that combines its content with content of another mutable string.
        /// </summary>
        private void Mutate(MutableString/*!*/ other) {
            RubyEncoding newEncoding = RequireCompatibleEncoding(other);
            Mutate();
            if (newEncoding != null) {
                SetEncoding(newEncoding);
            }
        }

        /// <summary>
        /// Checks if the other string's encoding is compatible with this string's encoding.
        /// If it is not an exception is thrown.
        /// Otherwise returns the encoding that should be used for the result of the operation if it is different from this string's encoding.
        /// (returns null if so).
        /// </summary>
        public RubyEncoding RequireCompatibleEncoding(MutableString/*!*/ other) {
            if (_encoding == other.Encoding) {
                return null;
            }

            // K-coded strings are in fact raw binary data which are just presented to .NET as strings (using their RealEncoding).
            if (_encoding.IsKCoding || other.Encoding.IsKCoding) {
                if (_encoding.RealEncoding == other.Encoding.RealEncoding) {
                    // it makes sense to present the resulting string using the same K-coding:
                    if (!_encoding.IsKCoding) {
                        return other.Encoding;
                    }
                    return null;
                } else {
                    // Present the result as raw binary data.
                    // Note: we could also preserve the K-coding for ascii strings but we don't do that to avoid additional cost.
                    SwitchToBytes();

                    // Must switch the other to bytes as well so that the operation is performed on bytes.
                    other.SwitchToBytes();

                    return RubyEncoding.Binary;
                }
            }

            // MRI implicitly changes encoding of a string that contains ascii bytes/characters only.
            if (other.IsAscii()) {
                if (IsBinaryEncoded && IsAscii()) {
                    // we can safely change encoding since the string contains ascii bytes/characters only:
                    return other.Encoding;
                } else {
                    return null;
                }
            }

            if (IsAscii()) {
                // we can safely change encoding since the string contains ascii bytes/characters only:
                return other.Encoding;
            }

            throw RubyExceptions.CreateEncodingCompatibilityError(_encoding, other.Encoding);
        }

        /// <summary>
        /// Changes encoding to the specified one. 
        /// </summary>
        public MutableString/*!*/ ChangeEncoding(RubyEncoding/*!*/ newEncoding, bool inplace) {
            ContractUtils.RequiresNotNull(newEncoding, "newEncoding");
            if (_encoding == newEncoding) {
                return this;
            }

            if ((_encoding.IsKCoding || _encoding == RubyEncoding.Binary) &&
                (newEncoding.IsKCoding || newEncoding == RubyEncoding.Binary)) {

                if (!IsAscii()) {
                    SwitchToBytes();
                }

                SetEncoding(newEncoding);
                return this;
            }

            MutableString result;
            if (inplace) {
                result = this;
            } else {
                result = MutableString.Create(this);
            }

            result.ChangeEncoding(newEncoding);
            return result;
        }

        private void ChangeEncoding(RubyEncoding/*!*/ newEncoding) {
            if (_encoding.RealEncoding == newEncoding.RealEncoding) {
                Mutate();
                SetEncoding(newEncoding); 
                return;
            }

            // this caches hash-code, which we need to invalidate due to encoding change:
            bool isAscii = IsAscii();
            Mutate();

            if (isAscii) {
                SetEncoding(newEncoding);
            } else if (newEncoding != RubyEncoding.Binary) {
                SwitchToCharacters();
                SetEncoding(newEncoding);
                CheckEncoding();
            } else {
                SwitchToBytes();
                SetEncoding(newEncoding);
            }
        }

        public override int GetHashCode() {
            if ((_flags & HashUnknownFlag) != 0) {
                UpdateHashCode();
            }

            return _hashCode;
        }

        public bool IsAscii() {
            var flags = _flags;

            if ((flags & AsciiUnknownFlag) != 0) {
                UpdateHashCode();
                flags = _flags;
            }

            return (flags & IsAsciiFlag) != 0;
        }

        public bool KnowsAscii {
            get { return (_flags & AsciiUnknownFlag) == 0; }
        }

        public bool KnowsHashCode {
            get { return (_flags & HashUnknownFlag) == 0; }
        }

        private void UpdateHashCode() {
            int hash;
            int binarySum;

            if (_encoding.IsKCoding) {
                // 1.8 strings don't know encodings => if 2 strings are binary equivalent they have the same hash:
                hash = _content.GetBinaryHashCode(out binarySum);
            } else {
                hash = _content.GetHashCode(out binarySum);

                // xor with the encoding if there are any non-ASCII characters in the string:
                if (binarySum >= 0x0080 && !IsBinaryEncoded) {
                    hash ^= _encoding.GetHashCode();
                }
            }

            if (binarySum >= 0x0080) {
                _flags = _flags & ~(AsciiUnknownFlag | HashUnknownFlag | IsAsciiFlag);
            } else {
                _flags = (_flags & ~(AsciiUnknownFlag | HashUnknownFlag)) | IsAsciiFlag;
            }

            _hashCode = hash;
        }

        public bool IsBinary {
            get { return _content.IsBinary; }
        }

        public bool IsBinaryEncoded {
            get {
                Debug.Assert((_encoding == RubyEncoding.Binary) == ((_flags & IsBinaryEncodedFlag) != 0));
                return (_flags & IsBinaryEncodedFlag) != 0; 
            }
        }

        public RubyEncoding/*!*/ Encoding {
            get { return _encoding; }
        }

        /// <summary>
        /// Checks if the string content is correctly encoded.
        /// </summary>
        public MutableString/*!*/ CheckEncoding() {
            try {
               return CheckEncodingInternal();
            } catch (EncoderFallbackException e) {
                throw RubyExceptions.CreateArgumentError(e, _encoding);
            } catch (DecoderFallbackException e) {
                throw RubyExceptions.CreateArgumentError(e, _encoding);
            }
        }

        internal MutableString/*!*/ CheckEncodingInternal() {
            _content.CheckEncoding();
            return this;
        }

        public bool IsTainted {
            get {
                return (_flags & IsTaintedFlag) != 0; 
            }
            set {
                var flags = _flags;
                if ((flags & IsFrozenFlag) != 0) {
                    throw RubyExceptions.CreateObjectFrozenError();
                }

                _flags = (flags & ~IsTaintedFlag) | (value ? IsTaintedFlag : 0);
            }
        }

        public bool IsFrozen {
            get {
                return (_flags & IsFrozenFlag) != 0;
            }
        }

        public bool HasChanged {
            get { return (_flags & HasChangedFlag) != 0; }
        }

        internal void ClearFlag(uint flag) {
            _flags &= ~flag;
        }

        internal bool IsFlagSet(uint flag) {
            return (_flags & flag) != 0;
        }

        public void TrackChanges() {
            _flags &= ~HasChangedFlag;
        }

        void IRubyObjectState.Freeze() {
            Freeze();
        }

        public MutableString/*!*/ Freeze() {
            _flags |= IsFrozenFlag;
            return this;
        }

        public void RequireNotFrozen() {
            if (IsFrozen) {
                throw RubyExceptions.CreateObjectFrozenError();
            }
        }

        /// <summary>
        /// Makes this string tainted if the specified string is tainted.
        /// </summary>
        public MutableString/*!*/ TaintBy(MutableString/*!*/ str) {
            IsTainted |= str.IsTainted;
            return this;
        }

        /// <summary>
        /// Makes this string tainted if the specified object is tainted.
        /// </summary>
        public MutableString/*!*/ TaintBy(IRubyObjectState/*!*/ obj) {
            IsTainted |= obj.IsTainted;
            return this;
        }

        /// <summary>
        /// Makes this string tainted if the specified object is tainted.
        /// </summary>
        public MutableString/*!*/ TaintBy(object/*!*/ obj, RubyContext/*!*/ context) {
            IsTainted |= context.IsObjectTainted(obj);
            return this;
        }

        /// <summary>
        /// Makes this string tainted if the specified object is tainted.
        /// </summary>
        public MutableString/*!*/ TaintBy(object/*!*/ obj, RubyScope/*!*/ scope) {
            IsTainted |= scope.RubyContext.IsObjectTainted(obj);
            return this;
        }

        #endregion

        #region Regular Expressions (read-only)

        internal MutableString/*!*/ EscapeRegularExpression() {
            return new MutableString(_content.EscapeRegularExpression(), _encoding);
        }

        #endregion

        #region Conversions (read-only)

        /// <summary>
        /// Returns a copy of the content in a form of an read-only string.
        /// The internal representation of the MutableString is preserved.
        /// </summary>
        /// <exception cref="DecoderFallbackException">Invalid characters present.</exception>
        public override string/*!*/ ToString() {
            return _content.ToString();
        }

        /// <summary>
        /// Switches the content to a byte array using the current encoding and decodes the binary into a string using the given encoding.
        /// </summary>
        /// <exception cref="DecoderFallbackException">Invalid characters present.</exception>
        public string/*!*/ ToString(Encoding/*!*/ encoding) {
            int count;
            byte[] bytes = _content.GetByteArray(out count);
            return encoding.GetString(bytes, 0, count);
        }

        /// <summary>
        /// Switches the content to a byte array using the current encoding and decodes the binary into a string using the given encoding.
        /// </summary>
        /// <exception cref="DecoderFallbackException">Invalid characters present.</exception>
        public string/*!*/ ToString(Encoding/*!*/ encoding, int start, int count) {
            byte[] bytes = GetByteArrayChecked(start, count);
            return encoding.GetString(bytes, start, count);
        }

        /// <summary>
        /// This property can be viewed using a string visualizer in a debugger, making it easy to inspect large or multi-line strings.
        /// </summary>
        internal string/*!*/ Dump {
            get { return ToString(); }
        }

        /// <summary>
        /// Returns a copy of the content in a form of an byte array.
        /// The internal representation of the MutableString is preserved.
        /// </summary>
        public byte[]/*!*/ ToByteArray() {
            return _content.ToByteArray();
        }

        /// <summary>
        /// Switches internal representation to textual.
        /// </summary>
        /// <returns>A copy of the internal representation unless it is read-only (string).</returns>
        public string/*!*/ ConvertToString() {
            return _content.ConvertToString();
        }

        /// <summary>
        /// Switches internal representation to binary.
        /// </summary>
        /// <returns>A copy of the internal representation.</returns>
        public byte[]/*!*/ ConvertToBytes() {
            return _content.ConvertToBytes();
        }

        public MutableString/*!*/ SwitchToBytes() {
            try {
                _content = _content.SwitchToBinaryContent();
            } catch (EncoderFallbackException e) {
                throw RubyExceptions.CreateArgumentError(e, _encoding);
            }
            return this;
        }

        /// <summary>
        /// Prepares the string for a character based operation.
        /// </summary>
        /// <exception cref="ArgumentException">
        /// String content is binary and contains byte sequence that doesn't represent a valid character.
        /// </exception>
        public MutableString/*!*/ SwitchToCharacters() {
            try {
                _content = _content.SwitchToStringContent();
            } catch (DecoderFallbackException e) {
                throw RubyExceptions.CreateArgumentError(e, _encoding);
            }
            return this;
        }

        /// <summary>
        /// Prepares the string for read-only character based operations.
        /// </summary>
        /// <exception cref="ArgumentException">
        /// String content is binary and contains byte sequence that doesn't represent a valid character.
        /// </exception>
        public MutableString/*!*/ PrepareForCharacterRead() {
            // Switch if the content is not already char based or the bytes are not the same as the equivalent characters:
            if (IsBinary && !IsBinaryEncoded && !IsAscii()) {
                SwitchToCharacters();
            } else if (_encoding.IsKCoding) {
                SwitchToBytes();
            }

            return this;
        }

        /// <summary>
        /// Prepares the string for mutating character based operations that can potentially write arbitrary characters to the string.
        /// </summary>
        /// <param name="asciiOnlyWrite">True if the operation writes ASCII characters only.</param>
        /// <exception cref="ArgumentException">
        /// String content is binary and contains byte sequence that doesn't represent a valid character.
        /// </exception>
        public MutableString/*!*/ PrepareForCharacterWrite() {
            if (IsBinary) {
                SwitchToCharacters();
            } else if (_encoding.IsKCoding) {
                SwitchToBytes();
            } else {
                _content.SwitchToMutableContent();
            }
            return this;
        }

        // used by auto-conversions
        public static explicit operator string(MutableString/*!*/ self) {
            return self._content.ConvertToString();
        }

        // used by auto-conversions
        public static explicit operator byte[](MutableString/*!*/ self) {
            return self._content.ConvertToBytes();
        }

        // used by auto-conversions
        public static explicit operator char(MutableString/*!*/ self) {
            try {
                return self.GetChar(0);
            } catch (IndexOutOfRangeException) {
                throw RubyExceptions.CreateTypeConversionError("String", "System::Char");
            }
        }

        #endregion

        #region Comparisons (read-only)

        public override bool Equals(object other) {
            return Equals(other as MutableString);
        }

        public bool Equals(MutableString other) {
            if (ReferenceEquals(this, other)) return true;
            if (ReferenceEquals(other, null)) return false;

            if (KnowsHashCode && other.KnowsHashCode && _hashCode != other._hashCode) {
                return false;
            }

            if (_encoding != other._encoding) {
                if (_encoding.CompareTo(other._encoding) != 0) {
                    // only ASCII strings might compare equal if their encodings are different:
                    if (!IsAscii() || !other.IsAscii()) {
                        return false;
                    }
                } else {
                    // we can't compare char representations if they are encoded with different k-codings:
                    Debug.Assert(_encoding.IsKCoding || other._encoding.IsKCoding);
                    SwitchToBytes();
                    other.SwitchToBytes();
                }
            }

            return _content.OrdinalCompareTo(other._content) == 0;
        }

        public int CompareTo(MutableString other) {
            if (ReferenceEquals(this, other)) return 0;
            if (ReferenceEquals(other, null)) return 1;

            if (_encoding != other._encoding) {
                int result = _encoding.CompareTo(other._encoding);
                if (result != 0) {
                    // only ASCII strings might compare equal if their encodings are different:
                    if (!IsAscii() || !other.IsAscii()) {
                        return result;
                    }
                } else {
                    // we can't compare char representations if they are encoded with different k-codings:
                    Debug.Assert(_encoding.IsKCoding || other._encoding.IsKCoding);
                    SwitchToBytes();
                    other.SwitchToBytes();
                }
            }

            return _content.OrdinalCompareTo(other._content);
        }

        #endregion

        #region Length (read-only)

        public static bool IsNullOrEmpty(MutableString/*!*/ str) {
            return ReferenceEquals(str, null) || str.IsEmpty;
        }

        public bool IsEmpty { 
            get { return _content.IsEmpty; } 
        }

        // TODO: replace by CharCount, ByteCount
        //[Obsolete("Use GetCharCount(), GetByteCount()")]
        public int Length {
            get { return _content.Count; }
        }
        
        public int GetLength() {
            return _content.Count;
        }

        public void SetLength(int value) {
            ContractUtils.Requires(value >= 0, "value");
            if (value < _content.Count) {
                _content.Remove(value, _content.Count - value);
            } else {
                _content.Count = value;
            }
        }

        public int GetCharCount() {
            return _content.GetCharCount();
        }

        public void SetCharCount(int value) {
            PrepareForCharacterRead().SetLength(value);
        }

        public int GetByteCount() {
            return _content.GetByteCount();
        }

        public void SetByteCount(int value) {
            SwitchToBytes().SetLength(value);
        }

        public MutableString/*!*/ TrimExcess() {
            _content.TrimExcess();
            return this;
        }

        public int Capacity { 
            get {
                return _content.GetCapacity();
            } set {
                _content.SetCapacity(value);
            }
        }

        public void EnsureCapacity(int minCapacity) {
            if (_content.GetCapacity() < minCapacity) {
                _content.SetCapacity(minCapacity);
            }
        }

        #endregion

        #region StartsWith, EndsWith (read-only)

        public bool StartsWith(char value) {
            return _content.StartsWith(value);
        }

        public bool EndsWith(char value) {
            return GetLastChar() == value;
        }
        
        public bool EndsWith(string/*!*/ value) {
            // TODO:
            return _content.ConvertToString().EndsWith(value, StringComparison.Ordinal);
        }

        public bool EndsWith(MutableString/*!*/ value) {
            ContractUtils.RequiresNotNull(value, "value");

            // TODO:
            if (IsBinary || value.IsBinary || _encoding.IsKCoding || value.Encoding.IsKCoding) {
                int valueLength = value.GetByteCount();
                int offset = GetByteCount() - valueLength;
                if (offset < 0) {
                    return false;
                }

                for (int i = 0; i < valueLength; i++) {
                    if (GetByte(offset + i) != value.GetByte(i)) {
                        return false;
                    }
                }
            } else {
                int valueLength = value.GetCharCount();
                int offset = GetCharCount() - valueLength;
                if (offset < 0) {
                    return false;
                }

                for (int i = 0; i < valueLength; i++) {
                    if (GetChar(offset + i) != value.GetChar(i)) {
                        return false;
                    }
                }
            }

            return true;
        }
        
        #endregion

        #region Enumerations (read-only)

        public struct Character : IEquatable<Character> {
            [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Security", "CA2105:ArrayFieldsShouldNotBeReadOnly")]
            public readonly byte[] Invalid;
            public readonly char Value;

            public bool IsValid {
                get { return Invalid == null; }
            }

            internal Character(byte[]/*!*/ invalid) {
                Invalid = invalid;
                Value = '\0';
            }

            internal Character(char value) {
                Invalid = null;
                Value = value;
            }

            public bool Equals(Character other) {
                if (IsValid) {
                    return other.IsValid && Value == other.Value;
                } else {
                    return !other.IsValid && Invalid.SequenceEqual(other.Invalid);
                }
            }
        }

        public abstract class CharacterEnumerator : IEnumerator<Character> {
            private readonly RubyEncoding/*!*/ _encoding;
            internal int _index;
            internal Character _current;

            internal CharacterEnumerator(RubyEncoding/*!*/ encoding) {
                Assert.NotNull(encoding);
                _encoding = encoding;
                _index = -1;
            }

            public Character Current {
                get {
                    if (_index < 0) {
                        throw new InvalidOperationException();
                    }
                    return _current;
                }
            }

            public virtual void Reset() {
                _index = -1;
                _current = default(Character);
            }

            internal void AppendTo(MutableString/*!*/ str) {
                ContractUtils.Requires(_encoding == str.Encoding);
                if (_index < 0) {
                    _index = 0;
                }

                AppendDataTo(str);

                Reset();
            }

            internal abstract void AppendDataTo(MutableString/*!*/ str);
            public abstract bool MoveNext();
            public abstract bool HasMore { get; }
            
            void IDisposable.Dispose() {
            }

            object System.Collections.IEnumerator.Current {
                get { return _current; }
            }
        }

        internal sealed class StringCharacterEnumerator : CharacterEnumerator {
            private readonly string/*!*/ _data;

            internal StringCharacterEnumerator(RubyEncoding/*!*/ encoding, string/*!*/ data)
                : base(encoding) {
                Assert.NotNull(data);
                _data = data;
            }

            public override bool HasMore {
                get { return _index < _data.Length; }
            }

            public override bool MoveNext() {
                if (_index < 0) {
                    _index = 0;
                }

                if (!HasMore) {
                    return false;
                }

                _current = new Character(_data[_index++]);
                return true;
            }

            internal override void AppendDataTo(MutableString/*!*/ str) {
                str.Append(_data, _index, _data.Length - _index);
            }
        }

        internal sealed class BinaryCharacterEnumerator : CharacterEnumerator {
            private readonly byte[]/*!*/ _data;
            private readonly int _count;

            internal BinaryCharacterEnumerator(RubyEncoding/*!*/ encoding, byte[]/*!*/ data, int count)
                : base(encoding) {
                Assert.NotNull(data);
                _data = data;
                _count = count;
            }

            public override bool HasMore {
                get { return _index < _count; }
            }

            public override bool MoveNext() {
                if (_index < 0) {
                    _index = 0;
                } 
                
                if (!HasMore) {
                    return false;
                }

                _current = new Character((char)_data[_index++]);
                return true;
            }

            internal override void AppendDataTo(MutableString/*!*/ str) {
                str.Append(_data, _index, _count - _index);
            }
        }

        internal sealed class CompositeCharacterEnumerator : CharacterEnumerator {
            private readonly char[]/*!*/ _data;
            private readonly int _count;
            private readonly List<byte[]> _invalid;
            private int _invalidIndex;

            internal CompositeCharacterEnumerator(RubyEncoding/*!*/ encoding, char[]/*!*/ data, int count, List<byte[]> invalid) 
                : base(encoding) {
                _data = data;
                _count = count;
                _invalid = invalid;
                _invalidIndex = 0;
            }

            private int InvalidCount {
                get { return _invalid != null ? _invalid.Count : 0; }
            }

            internal override void AppendDataTo(MutableString/*!*/ str) {
#if !SILVERLIGHT
                int i;
                while (_index < _count && _invalidIndex < InvalidCount) {
                    i = Array.IndexOf(_data, LosslessDecoderFallback.InvalidCharacterPlaceholder, _index);
                    str.Append(_data, _index, i - _index);
                    _index = i + 1;
                    str.Append(_invalid[_invalidIndex++]);
                }
#endif
                str.Append(_data, _index, _count - _index);
            }

            public override bool HasMore {
                get { return _index < _count; }
            }

            public override bool MoveNext() {
                if (_index < 0) {
                    _index = 0;
                }

                if (!HasMore) {
                    return false;
                }

                char c = _data[_index++];
#if SILVERLIGHT
                _current = new Character(c);
#else
                if (c != LosslessDecoderFallback.InvalidCharacterPlaceholder) {
                    _current = new Character(c);
                } else if (_invalidIndex < InvalidCount) {
                    _current = new Character(_invalid[_invalidIndex++]);
                } else {
                    // this can only happen if the decoder produces invalid characters \uFFFF, which it should not:
                    throw new InvalidOperationException("Decoder produced an invalid chracter \uFFFF.");
                }
#endif
                return true;
            }

            public override void Reset() {
                base.Reset();
                _invalidIndex = -1;
            }
        }

        internal static CharacterEnumerator/*!*/ EnumerateAsCharacters(byte[]/*!*/ data, int count, RubyEncoding/*!*/ encoding, out char[] allValid) {
#if SILVERLIGHT
            char[] chars = encoding.Encoding.GetChars(data, 0, count);
            allValid = null;
            return new CompositeCharacterEnumerator(encoding, chars, chars.Length, null);
#else
            Decoder decoder = encoding.Encoding.GetDecoder();
            var fallback = new LosslessDecoderFallback();
            decoder.Fallback = fallback;

            fallback.Track = true;
            char[] chars = new char[decoder.GetCharCount(data, 0, count, true)];
            
            fallback.Track = false;
            decoder.GetChars(data, 0, count, chars, 0, true);

            allValid = (fallback.InvalidCharacters == null) ? chars : null;
            return new CompositeCharacterEnumerator(encoding, chars, chars.Length, fallback.InvalidCharacters);
#endif
        }

        /// <summary>
        /// Enumerates over characters contained in the string. 
        /// Yields both valid characters and invalid byte sequences.
        /// Yields characters even for K-coded strings.
        /// </summary>
        public CharacterEnumerator/*!*/ GetCharacters() {
            return _content.GetCharacters();
        }

        public IEnumerable<byte>/*!*/ GetBytes() {
            return _content.GetBytes(); 
        }

        #endregion

        #region Slices (read-only)

        // converts the string representation to text if not already
        /// <exception cref="IndexOutOfRangeException">Index is out of range.</exception>
        public char GetChar(int index) {
            return _content.GetChar(index);
        }

        // converts the string representation to binary if not already
        /// <exception cref="IndexOutOfRangeException">Index is out of range.</exception>
        public byte GetByte(int index) {
            return _content.GetByte(index);
        }

        // returns -1 if the string is empty
        public int GetLastChar() {
            return (_content.IsEmpty) ? -1 : _content.GetChar(_content.GetCharCount() - 1); 
        }

        // returns -1 if the string is empty
        public int GetFirstChar() {
            return (_content.IsEmpty) ? -1 : _content.GetChar(0);
        }

        /// <summary>
        /// Returns a new mutable string containing a substring of the current one.
        /// </summary>
        public MutableString/*!*/ GetSlice(int start) {
            return GetSlice(start, Int32.MaxValue);
        }

        public MutableString/*!*/ GetSlice(int start, int count) {
            ContractUtils.Requires(start >= 0, "start");
            ContractUtils.Requires(count >= 0, "count");
            return new MutableString(_content.GetSlice(start, count), _encoding);
        }

        public string/*!*/ GetStringSlice(int start) {
            return GetStringSlice(start, Int32.MaxValue);
        }

        public string/*!*/ GetStringSlice(int start, int count) {
            ContractUtils.Requires(start >= 0, "start");
            ContractUtils.Requires(count >= 0, "count");
            return _content.GetStringSlice(start, count);
        }

        public byte[]/*!*/ GetBinarySlice(int start) {
            return GetBinarySlice(start, Int32.MaxValue);
        }

        public byte[]/*!*/ GetBinarySlice(int start, int count) {
            ContractUtils.Requires(start >= 0, "start");
            ContractUtils.Requires(count >= 0, "count");
            return _content.GetBinarySlice(start, count);
        }

        #endregion

        #region Split (read-only)

        // TODO: binary ops, ...
        public MutableString[]/*!*/ Split(char[]/*!*/ separators, int maxComponents, StringSplitOptions options) {
            // TODO:
            // TODO (encoding):
            return MakeArray(_content.ConvertToString().Split(separators, maxComponents, options), _encoding);
        }
        
        #endregion

        #region IndexOf (read-only)

        public int IndexOf(char value) {
            return IndexOf(value, 0);
        }

        public int IndexOf(char value, int start) {
            return IndexOf(value, start, Int32.MaxValue);
        }

        public int IndexOf(char value, int start, int count) {
            ContractUtils.Requires(start >= 0, "start");
            ContractUtils.Requires(count >= 0, "count");
            return _content.IndexOf(value, start, count);
        }

        public int IndexOf(byte value) {
            return IndexOf(value, 0);
        }

        public int IndexOf(byte value, int start) {
            return IndexOf(value, start, Int32.MaxValue);
        }

        public int IndexOf(byte value, int start, int count) {
            ContractUtils.Requires(start >= 0, "start");
            ContractUtils.Requires(count >= 0, "count");
            return _content.IndexOf(value, start, count);
        }

        public int IndexOf(string/*!*/ value) {
            return IndexOf(value, 0);
        }

        public int IndexOf(string/*!*/ value, int start) {
            return IndexOf(value, start, Int32.MaxValue);
        }

        public int IndexOf(string/*!*/ value, int start, int count) {
            ContractUtils.RequiresNotNull(value, "value");
            ContractUtils.Requires(start >= 0, "start");
            ContractUtils.Requires(count >= 0, "count");
            
            return _content.IndexOf(value, start, count);
        }

        public int IndexOf(byte[]/*!*/ value) {
            return IndexOf(value, 0);
        }

        public int IndexOf(byte[]/*!*/ value, int start) {
            return IndexOf(value, start, Int32.MaxValue);
        }

        public int IndexOf(byte[]/*!*/ value, int start, int count) {
            ContractUtils.RequiresNotNull(value, "value");
            ContractUtils.Requires(start >= 0, "start");
            ContractUtils.Requires(count >= 0, "count");
            
            return _content.IndexOf(value, start, count);
        }

        public int IndexOf(MutableString/*!*/ value) {
            return IndexOf(value, 0);
        }

        public int IndexOf(MutableString/*!*/ value, int start) {
            return IndexOf(value, start, Int32.MaxValue);
        }

        public int IndexOf(MutableString/*!*/ value, int start, int count) {
            ContractUtils.RequiresNotNull(value, "value");
            ContractUtils.Requires(start >= 0, "start");
            ContractUtils.Requires(count >= 0, "count");
            
            return value._content.IndexIn(_content, start, count);
        }

        #endregion

        #region LastIndexOf (read-only)

        public int LastIndexOf(char value) {
            return LastIndexOf(value, Int32.MaxValue - 1, Int32.MaxValue);
        }

        public int LastIndexOf(char value, int start) {
            return LastIndexOf(value, start, start + 1);
        }

        public int LastIndexOf(char value, int start, int count) {
            ContractUtils.Requires(start >= 0, "start");
            ContractUtils.Requires(count >= 0 && count - 1 <= start, "count");
            return _content.LastIndexOf(value, start, count);
        }

        public int LastIndexOf(byte value) {
            return LastIndexOf(value, Int32.MaxValue - 1, Int32.MaxValue);
        }

        public int LastIndexOf(byte value, int start) {
            return LastIndexOf(value, start, start + 1);
        }

        public int LastIndexOf(byte value, int start, int count) {
            ContractUtils.Requires(start >= 0, "start");
            ContractUtils.Requires(count >= 0 && count - 1 <= start, "count");
            return _content.LastIndexOf(value, start, count);
        }

        public int LastIndexOf(string/*!*/ value) {
            return LastIndexOf(value, Int32.MaxValue - 1, Int32.MaxValue);
        }

        public int LastIndexOf(string/*!*/ value, int start) {
            return LastIndexOf(value, start, start + 1);
        }

        public int LastIndexOf(string/*!*/ value, int start, int count) {
            ContractUtils.RequiresNotNull(value, "value");
            ContractUtils.Requires(start >= 0, "start");
            ContractUtils.Requires(count >= 0 && count - 1 <= start, "count");
            return _content.LastIndexOf(value, start, count);
        }

        public int LastIndexOf(byte[]/*!*/ value) {
            return LastIndexOf(value, Int32.MaxValue - 1, Int32.MaxValue);
        }

        public int LastIndexOf(byte[]/*!*/ value, int start) {
            return LastIndexOf(value, start, start + 1);
        }

        public int LastIndexOf(byte[]/*!*/ value, int start, int count) {
            ContractUtils.RequiresNotNull(value, "value");
            ContractUtils.Requires(start >= 0, "start");
            ContractUtils.Requires(count >= 0 && count - 1 <= start, "count");
            return _content.LastIndexOf(value, start, count);
        }

        public int LastIndexOf(MutableString/*!*/ value) {
            return LastIndexOf(value, Int32.MaxValue - 1, Int32.MaxValue);
        }

        public int LastIndexOf(MutableString/*!*/ value, int start) {
            return LastIndexOf(value, start, start + 1);
        }

        public int LastIndexOf(MutableString/*!*/ value, int start, int count) {
            ContractUtils.RequiresNotNull(value, "value");
            ContractUtils.Requires(start >= 0, "start");
            ContractUtils.Requires(count >= 0 && count - 1 <= start, "count");
            return value._content.LastIndexIn(_content, start, count);
        }

        #endregion

        #region Concat (read-only)

        /// <summary>
        /// Returns a concatenation of this string with other.
        /// </summary>
        public MutableString/*!*/ Concat(MutableString/*!*/ other) {
            ContractUtils.RequiresNotNull(other, "other");
            var encoding = RequireCompatibleEncoding(other) ?? _encoding;
            return new MutableString(_content.Concat(other._content), encoding);
        }

        #endregion

        #region Append

        /// <summary>
        /// Value should only contain characters that can be represented in the string's encoding.
        /// </summary>
        public MutableString/*!*/ Append(char value) {
            #region Optimization: MutateOne inlined
            uint flags = _flags;
            if ((flags & IsFrozenFlag) != 0) {
                throw RubyExceptions.CreateObjectFrozenError();
            }
            _flags = flags | (value >= 0x80 ? HasChangedFlags | HashUnknownFlag | AsciiUnknownFlag : HasChangedFlags | HashUnknownFlag);
            #endregion

            _content.Append(value, 1);
            return this;
        }

        /// <summary>
        /// Value should only contain characters that can be represented in the string's encoding.
        /// </summary>
        public MutableString/*!*/ Append(char value, int repeatCount) {
            #region Optimization: MutateOne inlined
            uint flags = _flags;
            if ((flags & IsFrozenFlag) != 0) {
                throw RubyExceptions.CreateObjectFrozenError();
            }
            _flags = flags | (value >= 0x80 ? HasChangedFlags | HashUnknownFlag | AsciiUnknownFlag : HasChangedFlags | HashUnknownFlag);
            #endregion

            _content.Append(value, repeatCount);
            return this;
        }

        public MutableString/*!*/ Append(byte value) {
            #region Optimization: MutateOne inlined
            uint flags = _flags;
            if ((flags & IsFrozenFlag) != 0) {
                throw RubyExceptions.CreateObjectFrozenError();
            }
            _flags = flags | (value >= 0x80 ? HasChangedFlags | HashUnknownFlag | AsciiUnknownFlag : HasChangedFlags | HashUnknownFlag);
            #endregion

            _content.Append(value, 1);
            return this;
        }

        public MutableString/*!*/ Append(byte value, int repeatCount) {
            #region Optimization: MutateOne inlined
            uint flags = _flags;
            if ((flags & IsFrozenFlag) != 0) {
                throw RubyExceptions.CreateObjectFrozenError();
            }
            _flags = flags | (value >= 0x80 ? HasChangedFlags | HashUnknownFlag | AsciiUnknownFlag : HasChangedFlags | HashUnknownFlag);
            #endregion

            _content.Append(value, repeatCount);
            return this;
        }

        /// <summary>
        /// Value should only contain characters that can be represented in the string's encoding.
        /// </summary>
        public MutableString/*!*/ Append(char[] value) {
            if (value != null) {
                Mutate();
                _content.Append(value, 0, value.Length);
            }
            return this;
        }

        /// <summary>
        /// Value should only contain characters that can be represented in the string's encoding.
        /// </summary>
        public MutableString/*!*/ Append(char[]/*!*/ value, int start, int count) {
            ContractUtils.RequiresNotNull(value, "value");
            ContractUtils.RequiresArrayRange(value, start, count, "startIndex", "count");

            Mutate();
            _content.Append(value, start, count);
            return this;
        }

        /// <summary>
        /// Value should only contain characters that can be represented in the string's encoding.
        /// </summary>
        public MutableString/*!*/ Append(string value) {
            if (value != null) {
                Mutate();
                _content.Append(value, 0, value.Length);
            }
            return this;
        }

        /// <summary>
        /// Value should only contain characters that can be represented in the string's encoding.
        /// </summary>
        public MutableString/*!*/ Append(string/*!*/ value, int start, int count) {
            ContractUtils.RequiresNotNull(value, "value");
            ContractUtils.RequiresArrayRange(value.Length, start, count, "start", "count");
            Mutate();

            _content.Append(value, start, count);
            return this;
        }

        public MutableString/*!*/ Append(byte[] value) {
            if (value != null) {
                Mutate();
                _content.Append(value, 0, value.Length);
            }
            return this;
        }

        public MutableString/*!*/ Append(byte[]/*!*/ value, int start, int count) {
            ContractUtils.RequiresNotNull(value, "value");
            ContractUtils.RequiresArrayRange(value, start, count, "start", "count");

            Mutate();
            _content.Append(value, start, count);
            return this;
        }

        /// <summary>
        /// Reads at most "count" bytes from "source" stream and appends them to this string.
        /// Allocates space for "count" bytes, so the string might need to be trimmed after the operation.
        /// </summary>
        public MutableString/*!*/ Append(Stream/*!*/ stream, int count) {
            ContractUtils.RequiresNotNull(stream, "stream");
            ContractUtils.Requires(count >= 0, "count");

            Mutate();
            _content.Append(stream, count);
            return this;
        }

        public MutableString/*!*/ Append(MutableString value) {
            if ((object)value != null) {
                Mutate(value);
                _content.Append(value._content, 0, value._content.Count);
            }
            return this;
        }

        public MutableString/*!*/ Append(MutableString/*!*/ value, int start) {
            return Append(value, start, value._content.Count - start);
        }

        /// <summary>
        /// Appends a substring of a given string to this string.
        /// <c>start</c> and <c>count</c> are specified
        /// in characters if the <c>value</c> is represented in characters and 
        /// in bytes if the <c>value</c> is represented in bytes.
        /// </summary>
        public MutableString/*!*/ Append(MutableString/*!*/ value, int start, int count) {
            ContractUtils.RequiresNotNull(value, "value");
            ContractUtils.Requires(start >= 0, "start");
            ContractUtils.Requires(count >= 0, "count");

            Mutate(value);
            _content.Append(value._content, start, count);
            return this;
        }

        public MutableString/*!*/ AppendMultiple(MutableString/*!*/ value, int repeatCount) {
            ContractUtils.RequiresNotNull(value, "value");
            Mutate(value);

            // TODO: we can do better here (double the amount of copied bytes/chars in each iteration)
            var other = value._content;
            EnsureCapacity(other.Count * repeatCount);
            while (repeatCount-- > 0) {
                _content.Append(other, 0, other.Count);
            }
            return this;
        }

        /// <summary>
        /// Format and values should only contain characters that can be represented in the string's encoding.
        /// </summary>
        public MutableString/*!*/ AppendFormat(string/*!*/ format, params object[] args) {
            ContractUtils.RequiresNotNull(format, "format");
            Mutate();

            _content.AppendFormat(CultureInfo.InvariantCulture, format, args);
            return this;
        }

        public MutableString/*!*/ Append(Character character) {
            if (character.IsValid) {
                return Append(character.Value);
            } else {
                return Append(character.Invalid);
            }
        }

        public MutableString/*!*/ AppendRemaining(CharacterEnumerator/*!*/ characters) {
            ContractUtils.RequiresNotNull(characters, "characters");
            characters.AppendTo(this);
            return this;
        }

        #endregion

        #region Insert // TODO: Insert(MS) like Append(MS)

        public void SetChar(int index, char value) {
            #region Optimization: MutateOne inlined
            if ((_flags & IsFrozenFlag) != 0) {
                throw RubyExceptions.CreateObjectFrozenError();
            }
            _flags |= (value >= 0x80 ? HasChangedFlags | HashUnknownFlag | AsciiUnknownFlag : HasChangedFlags | HashUnknownFlag);
            #endregion

            _content.SetChar(index, value);
        }

        public void SetByte(int index, byte value) {
            #region Optimization: MutateOne inlined
            uint flags = _flags;
            if ((flags & IsFrozenFlag) != 0) {
                throw RubyExceptions.CreateObjectFrozenError();
            }
            _flags = flags | (value >= 0x80 ? HasChangedFlags | HashUnknownFlag | AsciiUnknownFlag : HasChangedFlags | HashUnknownFlag);
            #endregion

            _content.SetByte(index, value);
        }

        public MutableString/*!*/ Insert(int index, char value) {
            #region Optimization: MutateOne inlined
            uint flags = _flags;
            if ((flags & IsFrozenFlag) != 0) {
                throw RubyExceptions.CreateObjectFrozenError();
            }
            _flags = flags | (value >= 0x80 ? HasChangedFlags | HashUnknownFlag | AsciiUnknownFlag : HasChangedFlags | HashUnknownFlag);
            #endregion

            _content.Insert(index, value);
            return this;
        }

        public MutableString/*!*/ Insert(int index, byte value) {
            #region Optimization: MutateOne inlined
            uint flags = _flags;
            if ((flags & IsFrozenFlag) != 0) {
                throw RubyExceptions.CreateObjectFrozenError();
            }
            _flags = flags | (value >= 0x80 ? HasChangedFlags | HashUnknownFlag | AsciiUnknownFlag : HasChangedFlags | HashUnknownFlag);
            #endregion

            _content.Insert(index, value);
            return this;
        }

        public MutableString/*!*/ Insert(int index, string value) {
            //RequiresArrayInsertIndex(index);
            if (value != null) {
                Mutate();
                _content.Insert(index, value, 0, value.Length);
            }
            return this;
        }

        public MutableString/*!*/ Insert(int index, string/*!*/ value, int start, int count) {
            //RequiresArrayInsertIndex(index);
            ContractUtils.RequiresNotNull(value, "value");
            ContractUtils.RequiresArrayRange(value.Length, start, count, "start", "count");

            Mutate();
            _content.Insert(index, value, start, count);
            return this;
        }

        public MutableString/*!*/ Insert(int index, byte[] value) {
            //RequiresArrayInsertIndex(index);
            if (value != null) {
                Mutate();
                _content.Insert(index, value, 0, value.Length);
            }
            return this;
        }

        public MutableString/*!*/ Insert(int index, byte[]/*!*/ value, int start, int count) {
            //RequiresArrayInsertIndex(index);
            ContractUtils.RequiresNotNull(value, "value");
            ContractUtils.RequiresArrayRange(value, start, count, "start", "count");

            Mutate();
            _content.Insert(index, value, start, count);
            return this;
        }

        public MutableString/*!*/ Insert(int index, MutableString value) {
            //RequiresArrayInsertIndex(index);
            if (value != null) {
                Mutate(value);
                value._content.InsertTo(_content, index, 0, value._content.Count);
            }
            return this;
        }

        // TODO: start, count measured in characters or bytes?
        public MutableString/*!*/ Insert(int index, MutableString/*!*/ value, int start, int count) {
            //RequiresArrayInsertIndex(index);
            ContractUtils.RequiresNotNull(value, "value");
            //value.RequiresArrayRange(start, count);

            Mutate(value);
            value._content.InsertTo(_content, index, start, count);
            return this;
        }

        #endregion

        #region Reverse

        public MutableString/*!*/ Reverse() {
            MutatePreserveAsciiness();
            PrepareForCharacterWrite();

            // TODO: surrogates
            var content = _content;

            int length = content.Count;
            if (length <= 1) {
                return this;
            }

            for (int i = 0; i < length / 2; i++) {
                char a = content.GetChar(i);
                char b = content.GetChar(length - i - 1);
                content.SetChar(i, b);
                content.SetChar(length - i - 1, a);
            }

            Debug.Assert(content == _content);
            return this;
        }

        #endregion

        #region Replace, Write, Remove, Trim, Clear, Translate, TranslateSqueeze, TranslateRemove

        public MutableString/*!*/ Replace(int start, int count, MutableString value) {
            //RequiresArrayRange(start, count);

            // TODO:
            Mutate(value);
            return Remove(start, count).Insert(start, value);
        }

        // TODO: characters
        public MutableString/*!*/ WriteBytes(int offset, MutableString/*!*/ value, int start, int count) {
            byte[] bytes = value.GetByteArrayChecked(start, count);
            return Write(offset, bytes, start, count);
        }

        public MutableString/*!*/ Write(int offset, byte[]/*!*/ value, int start, int count) {
            Mutate();
            _content.Write(offset, value, start, count);
            return this;
        }

        public MutableString/*!*/ Write(int offset, byte/*!*/ value, int repeatCount) {
            Mutate();
            _content.Write(offset, value, repeatCount);
            return this;
        }

        public MutableString/*!*/ Remove(int start) {
            ContractUtils.Requires(start >= 0, "start");
            MutateRemove();
            _content.Remove(start, _content.Count - start);
            return this;
        }

        public MutableString/*!*/ Remove(int start, int count) {
            ContractUtils.Requires(start >= 0, "start");
            ContractUtils.Requires(count >= 0, "count");
            MutateRemove();
            _content.Remove(start, count);
            return this;
        }

        public MutableString/*!*/ Trim(int start, int count) {
            ContractUtils.Requires(start >= 0, "start");
            ContractUtils.Requires(count >= 0, "count");
            MutateRemove();
            _content = _content.GetSlice(start, count);
            return this;
        }

        public MutableString/*!*/ Clear() {
            Mutate();
            _content = _content.GetSlice(0, 0);
            return this;
        }

        private static void PrepareTranslation(MutableString/*!*/ src, MutableString/*!*/ dst, CharacterMap/*!*/ map) {
            ContractUtils.RequiresNotNull(src, "src");
            ContractUtils.RequiresNotNull(dst, "dst");
            ContractUtils.RequiresNotNull(map, "map");
            ContractUtils.Requires(ReferenceEquals(src, dst) || dst.IsEmpty);

            dst.Mutate();
            dst.PrepareForCharacterWrite();

            if (!ReferenceEquals(src, dst)) {
                src.PrepareForCharacterRead();
                dst.SetLength(src.GetLength());
            }
        }

        public static bool Translate(MutableString/*!*/ src, MutableString/*!*/ dst, CharacterMap/*!*/ map) {
            PrepareTranslation(src, dst, map);
            ContractUtils.Requires(map.HasFullMap, "map");

            int srcLength = src.GetCharCount();
            var dstContent = dst._content;
            var srcContent = src._content;

            bool anyMaps = false;
            bool inplace = ReferenceEquals(src, dst);
            
            for (int i = 0; i < srcLength; i++) {
                char s = srcContent.GetChar(i);
                int m = map.TryMap(s);
                if (m >= 0) {
                    anyMaps = true;
                    dstContent.SetChar(i, (char)m);
                } else if (!inplace) {
                    dstContent.SetChar(i, s);
                }
            }

            Debug.Assert(dstContent == dst._content && srcContent == src._content);
            Debug.Assert(!dst.KnowsAscii && !dst.KnowsHashCode);
            return anyMaps;
        }

        public static bool TranslateSqueeze(MutableString/*!*/ src, MutableString/*!*/ dst, CharacterMap/*!*/ map) {
            PrepareTranslation(src, dst, map);
            ContractUtils.Requires(map.HasFullMap, "map");

            int srcLength = src.GetCharCount();
            var dstContent = dst._content;
            var srcContent = src._content;

            bool anyMaps = false;
            int j = 0;
            int last = -1;
            for (int i = 0; i < srcLength; i++) {
                char s = srcContent.GetChar(i);
                int m = map.TryMap(s);
                if (m >= 0) {
                    anyMaps = true;
                    if (m != last) {
                        dstContent.SetChar(j++, (char)m);
                    }
                } else {
                    dstContent.SetChar(j++, s);
                }
                last = m;
            }

            if (j < srcLength) {
                dst.Remove(j);
            }

            Debug.Assert(dstContent == dst._content && srcContent == src._content);
            Debug.Assert(!dst.KnowsAscii && !dst.KnowsHashCode);
            return anyMaps;
        }

        public static bool TranslateRemove(MutableString/*!*/ src, MutableString/*!*/ dst, CharacterMap/*!*/ map) {
            PrepareTranslation(src, dst, map);
            ContractUtils.Requires(map.HasBitmap, "map");

            var dstContent = dst._content;
            var srcContent = src._content;
            int srcLength = src.GetCharCount();

            bool remove = !map.IsComplemental;
            bool anyMaps = false;
            int j = 0;
            for (int i = 0; i < srcLength; i++) {
                char s = srcContent.GetChar(i);
                if (map.IsMapped(s) == remove) {
                    anyMaps = true;
                } else {
                    dstContent.SetChar(j++, s);
                }
            }

            if (j < srcLength) {
                dst.Remove(j);
            }

            Debug.Assert(dstContent == dst._content && srcContent == src._content);
            Debug.Assert(!dst.KnowsAscii && !dst.KnowsHashCode);
            return anyMaps;
        }

        #endregion

        #region Quoted Representation (read-only)

#if !SILVERLIGHT
        private sealed class DumpDecoderFallback : DecoderFallback {
            // \xXX
            // \000
            private const int ReplacementLength = 4;

            // We can't emit backslash directly since it would be escaped by subsequent processing.
            internal const char EscapePlaceholder = '\uffff';

            private readonly bool _octalEscapes;

            public DumpDecoderFallback(bool octalEscapes) {
                _octalEscapes = octalEscapes;
            }

            public override DecoderFallbackBuffer/*!*/ CreateFallbackBuffer() {
                return new Buffer(this);
            }

            public override int MaxCharCount {
                get { return ReplacementLength; }
            }

            internal sealed class Buffer : DecoderFallbackBuffer {
                private readonly DumpDecoderFallback _fallback;
                private int _index;
                private byte[] _bytes;

                public Buffer(DumpDecoderFallback/*!*/ fallback) {
                    _fallback = fallback;
                }

                public bool HasInvalidCharacters {
                    get { return _bytes != null; }
                }

                public override bool Fallback(byte[]/*!*/ bytesUnknown, int index) {
                    _bytes = bytesUnknown;
                    _index = 0;
                    return true;
                }

                public override char GetNextChar() {
                    if (Remaining == 0) {
                        return '\0';
                    }

                    int state = _index % ReplacementLength;
                    int b = _bytes[_index / ReplacementLength];
                    _index++;

                    if (_fallback._octalEscapes) {
                        switch (state) {
                            case 0: return EscapePlaceholder;
                            case 1: return (char)('0' + (b >> 6));
                            case 2: return (char)('0' + ((b >> 3) & 7));
                            case 3: return (char)('0' + (b & 7));
                        }
                    } else {
                        switch (state) {
                            case 0: return EscapePlaceholder;
                            case 1: return 'x';
                            case 2: return (b >> 4).ToUpperHexDigit();
                            case 3: return (b & 0xf).ToUpperHexDigit();
                        }
                    }

                    throw Assert.Unreachable;
                }

                public override bool MovePrevious() {
                    if (_index == 0) {
                        return false;
                    }
                    _index--;
                    return true;
                }

                public override int Remaining {
                    get { return _bytes.Length * ReplacementLength - _index; }
                }

                public override void Reset() {
                    _index = 0;
                }
            }
        }

        private static string/*!*/ ToStringWithEscapedInvalidCharacters(byte[]/*!*/ bytes, Encoding/*!*/ encoding, bool octalEscapes, out int escapePlaceholder) {
            var decoder = encoding.GetDecoder();
            decoder.Fallback = new DumpDecoderFallback(octalEscapes);
            char[] chars = new char[decoder.GetCharCount(bytes, 0, bytes.Length, true)];
            decoder.GetChars(bytes, 0, bytes.Length, chars, 0, true);
            escapePlaceholder = ((DumpDecoderFallback.Buffer)decoder.FallbackBuffer).HasInvalidCharacters ? DumpDecoderFallback.EscapePlaceholder : -1;
            return new String(chars);
        }
#else
        private static string/*!*/ ToStringWithEscapedInvalidCharacters(byte[]/*!*/ bytes, Encoding/*!*/ encoding, bool octalEscapes, out int escapePlaceholder) {
            // Silverlight doens't support fallbacks, just replace invalid characters with the default replacement:
            escapePlaceholder = -1;
            return new String(encoding.GetChars(bytes));
        }
#endif

        // TODO: make struct and include quote character
        [Flags]
        public enum Escape {
            Default = 0,

            /// <summary>
            /// Escape all non-ASCII characters.
            /// </summary>
            NonAscii = 1,

            /// <summary>
            /// Escape #{, #$, #@ and \.
            /// </summary>
            Special = 2,

            /// <summary>
            /// Use octal escapes. Hexadecimal are used if not specified.
            /// </summary>
            Octal = 4
        }

        private static void AppendBinaryCharRepresentation(StringBuilder/*!*/ result, int currentChar, int nextChar, Escape escape, int quote) {

            Debug.Assert(currentChar >= 0 && currentChar <= 0x00ff);
            switch (currentChar) {
                case '\a': result.Append("\\a"); break;
                case '\b': result.Append("\\b"); break;
                case '\t': result.Append("\\t"); break;
                case '\n': result.Append("\\n"); break;
                case '\v': result.Append("\\v"); break;
                case '\f': result.Append("\\f"); break;
                case '\r': result.Append("\\r"); break;
                case 27: result.Append("\\e"); break;
                case '\\':
                    if ((escape & Escape.Special) != 0) {
                        result.Append("\\\\");
                    } else {
                        result.Append('\\');
                    }
                    return;

                case '#':
                    if ((escape & Escape.Special) != 0) {
                        switch (nextChar) {
                            case '{':
                            case '$':
                            case '@':
                                result.Append('\\');
                                break;
                        }
                    }
                    result.Append('#');
                    break;

                default:
                    if (currentChar == quote) {
                        result.Append('\\');
                        result.Append((char)quote);
                    } else if (currentChar < 0x0020 || currentChar >= 0x080 && (escape & Escape.NonAscii) != 0) {
                        if ((escape & Escape.Octal) != 0) {
                            AppendOctalEscape(result, currentChar);
                        } else {
                            AppendHexEscape(result, currentChar);
                        }
                    } else {
                        result.Append((char)currentChar);
                    }
                    break;
            }
        }

        public static int AppendUnicodeCharRepresentation(StringBuilder/*!*/ result, int currentChar, int nextChar, Escape escape, 
            int quote, int escapePlaceholder) {

            int inc = 1;
            if (currentChar == escapePlaceholder) {
                result.Append('\\');
            } else if (currentChar < 0x0080) {
                AppendBinaryCharRepresentation(result, currentChar, nextChar, escape, quote);
            } else if ((escape & Escape.NonAscii) != 0) {
                if (nextChar != -1 && Char.IsSurrogatePair((char)currentChar, (char)nextChar)) {
                    currentChar = Tokenizer.ToCodePoint(currentChar, nextChar);
                    inc = 2;
                }
                result.Append("\\u{");
                result.Append(Convert.ToString(currentChar, 16));
                result.Append('}');
            } else if (nextChar != -1 && Char.IsSurrogatePair((char)currentChar, (char)nextChar)) {
                result.Append((char)currentChar);
                result.Append((char)nextChar);
                inc = 2;
            } else if (Char.IsSurrogate((char)currentChar)) {
                // we have to escape - the character is incomplete:
                result.Append("\\u{");
                result.Append(Convert.ToString(currentChar, 16));
                result.Append('}');
            } else {
                result.Append((char)currentChar);
            }
            return inc;
        }

        public static void AppendCharRepresentation(StringBuilder/*!*/ result, int currentChar, int nextChar, Escape escape, 
            int quote, int escapePlaceholder) {

            if (currentChar == escapePlaceholder) {
                result.Append('\\');
            } else if (currentChar < 0x0100) {
                AppendBinaryCharRepresentation(result, currentChar, nextChar, escape, quote);
            } else {
                result.Append((char)currentChar);
            }
        }

        private static void AppendHexEscape(StringBuilder/*!*/ result, int c) {
            result.Append("\\x");
            result.Append((c >> 4).ToUpperHexDigit());
            result.Append((c & 0xf).ToUpperHexDigit());
        }

        private static void AppendOctalEscape(StringBuilder/*!*/ result, int c) {
            result.Append('\\');
            result.Append((char)('0' + (c >> 6)));
            result.Append((char)('0' + ((c >> 3) & 7)));
            result.Append((char)('0' + (c & 7)));
        }

        private string/*!*/ ToStringWithEscapedInvalidCharacters(RubyEncoding/*!*/ encoding, bool octalEscapes, out int escapePlaceholder) {
            Debug.Assert(encoding != RubyEncoding.Binary);
            if (IsBinary || encoding != _encoding) {
                return ToStringWithEscapedInvalidCharacters(ToByteArray(), encoding.Encoding, octalEscapes, out escapePlaceholder);
            } else {
                escapePlaceholder = -1;
                return ToString();
            }
        }

        /// <summary>
        /// Returns a string with all non-ASCII characters replaced by escaped Unicode or hexadecimal numeric sequences.
        /// </summary>
        public string/*!*/ ToAsciiString(bool octalEscapes) {
            Escape escape = Escape.NonAscii | (octalEscapes ? Escape.Octal : 0);
            var result = AppendRepresentation(new StringBuilder(), null, escape, -1).ToString();
            Debug.Assert(result.IsAscii());
            return result;
        }

        /// <summary>
        /// Returns a copy of the content in a form of a read-only UTF16 string with escaped invalid characters.
        /// </summary>
        public string/*!*/ ToStringWithEscapedInvalidCharacters(RubyEncoding/*!*/ encoding) {
            ContractUtils.RequiresNotNull(encoding, "encoding");
            return AppendRepresentation(new StringBuilder(), encoding, Escape.Default, -1).ToString();
        }

        public StringBuilder/*!*/ AppendRepresentation(StringBuilder/*!*/ result, RubyEncoding forceEncoding, Escape escape, int quote) {
            ContractUtils.RequiresNotNull(result, "result");

            RubyEncoding encoding = forceEncoding ?? _encoding;

            if (encoding == RubyEncoding.Binary || ((escape & Escape.NonAscii) != 0 && encoding != RubyEncoding.UTF8)) {
                escape |= Escape.NonAscii;
                if (IsBinary) {
                    AppendBinaryRepresentation(result, ToByteArray(), escape, quote);
                } else {
                    AppendStringRepresentation(result, ToString(), escape, quote, -1);
                }
            } else {
                int escapePlaceholder;
                var str = ToStringWithEscapedInvalidCharacters(encoding, (escape & Escape.Octal) != 0, out escapePlaceholder);

                if (encoding == RubyEncoding.UTF8) {
                    AppendUnicodeRepresentation(result, str, escape, quote, escapePlaceholder);
                } else {
                    AppendStringRepresentation(result, str, escape, quote, escapePlaceholder);
                }
            }
            
            return result;
        }

        public static StringBuilder/*!*/ AppendUnicodeRepresentation(StringBuilder/*!*/ result, string/*!*/ str, Escape escape,
            int quote, int escapePlaceholder) {

            int i = 0;
            while (i < str.Length) {
                i += AppendUnicodeCharRepresentation(result, (int)str[i], (i < str.Length - 1) ? (int)str[i + 1] : -1, escape, quote, escapePlaceholder);
            }

            return result;
        }

        public static StringBuilder/*!*/ AppendStringRepresentation(StringBuilder/*!*/ result, string/*!*/ str, Escape escape,
            int quote, int escapePlaceholder) {
            for (int i = 0; i < str.Length; i++) {
                AppendCharRepresentation(result, (int)str[i], (i < str.Length - 1) ? (int)str[i + 1] : -1, escape, quote, escapePlaceholder);
            }
            return result;
        }

        public static StringBuilder/*!*/ AppendBinaryRepresentation(StringBuilder/*!*/ result, byte[]/*!*/ bytes, Escape escape, int quote) {
            for (int i = 0; i < bytes.Length; i++) {
                AppendCharRepresentation(result, (int)bytes[i], (i < bytes.Length - 1) ? (int)bytes[i + 1] : -1, escape, quote, -1);
            }
            return result;
        }

        internal string/*!*/ GetDebugValue() {
            return AppendRepresentation(new StringBuilder(), null, MutableString.Escape.Default, '"').ToString();
        }

        internal string/*!*/ GetDebugType() {
            if (!IsBinary) {
                return "String (" + _encoding.ToString() + ")";
            } else if (_encoding != RubyEncoding.Binary) {
                return "String (binary/" + _encoding.ToString() + ")";
            } else {
                return "String (binary)";
            }
        }

        #endregion

        #region FormatMessage (read-only)

        /// <summary>
        /// Formats an error message that can be loaded from resources and thus localized.
        /// </summary>
        public static MutableString/*!*/ FormatMessage(string/*!*/ message, params MutableString[]/*!*/ args) {
            return MutableString.Create(String.Format(message, args), RubyEncoding.UTF8);
        }

        #endregion

        #region Internal Helpers

        internal byte[]/*!*/ GetByteArray(out int count) {
            return _content.GetByteArray(out count);
        }

        internal byte[]/*!*/ GetByteArrayChecked(int start, int count) {
            int byteCount;
            var result = _content.GetByteArray(out byteCount);
            if (count < 0 || start > byteCount - count) {
                throw new ArgumentOutOfRangeException("count");
            }
            return result;
        }

        #endregion
    }
}
