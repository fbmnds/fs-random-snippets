Thank you for downloading PBKDF2.NET v2.0.0!

PBKDF2.NET provides adaptive password-based key derivation functionality (PBKDF2) for the .NET Framework.
It adheres to the suggested PBKDF2 implementation while also holding true to the .NET cryptographic
programming model. This is done to ensure smooth transition for users of this library and reducing the amount
of refactoring involved to integrate this library into existing source.

PBKDF2 extends two core .NET namespaces:

	System.Security.Cryptography and System.Configuration

The choice to extend these .NET namespaces as opposed to creation our own namespace was to allow easy
integration with existing source, reducing refactorization. The public types introduced in this library are:

	System.Security.Cryptography.PBKDF2
	System.Configuration.PBKDF2Section
	
The PBKDF2 type is the class that performs the PBKDF2 function. It's structure is similar to familiar .NET types
such as Rfc2898DeriveBytes and the DeriveBytes base class, with the addition of the HashName property, which is a
string that accepts the friendly name of a HMAC-based hashing implementation, and the Password property, which provides
access to the Password used for the current operation as an array of bytes. The PBKDF2 class also adds a few additional
type constructors to allow more versatile initialization calls to suit your needs.

The PBKDF2Section type is the configuration section which supplies default values for the PBKDF2 class. With this, you
can set defaults for HashName, IterationCount and SaltSize. It's corresponding configuration section is the <pbkdf2> element,
and it's attributes are hashName, iterationCount and saltSize. These values are used if you choose to omit the respective
parameters from the constructor during initialization of an instance of a PBKDF2 object. This allows the type to have
default settings application-wide. The following ranges of values are allowed for configuration:

	iterationCount: MinValue = 1, MaxValue = int.MaxValue
	
	saltSize: minValue = 8, MaxValue = 65536
	
	hashName: any valid friendly name of an HMAC hashing implementation registered with the application domain.

All built-in .NET HMAC-based hashing implementations are supported for hashName.
To configure hashName to use a custom hashing implementation, you must be sure the type inherits from 
System.Security.Cryptography.HMAC and that the type is registered with the application domain (for example: via the
CryptoConfig class).

For details on how to use this library, please refer to the project site at:

	http://therealmagicmike.github.io/PBKDF2.NET/

This library and it's source is licensed under the MIT License, which is included with this download.
Full source code and documentation is available at:

	http://github.com/therealmagicmike/PBKDF2.NET

***** Change Log *****

		v2.0.0.0
========================
The following changes were introduced in this release:

* improved type interface for all types in lib
* some names of public properties on types were changed drastically from version 1
* library (.dll) naming convention has been changed to provide smoother transitions in future releases
	Details: The naming convention of this library is now simply "PBKDF2.NET". Revisions to the source
	structuring to accommodate possible builds targeting specific framework versions.
* internal initialization process has changed within the System.Security.Cryptography.PBKDF2 class.
	Details: This was done both for performance-tuning reasons and to accommodate the addition of byte[]
	Password public property being added to the type to be treated like a Key property. These changes were
	necessary to integrate these changes with the internal state management of PBKDF2 objects.
