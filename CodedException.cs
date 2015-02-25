
using System;


class CodedException : ApplicationException {

	public Int32 StatusCode { get; private set; }

	public CodedException(Int32 status_code = 1, Exception inner = null, String message = null)
		: base(message, inner)
	{
		this.StatusCode = status_code;
	}

}
