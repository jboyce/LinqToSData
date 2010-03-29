DESCRIPTION	
This is an early version of a linq to SData provider.
It allows you to write linq queries in .Net, which get translated into SData queries, sent to an SData portal and returned as Sage.Entity.Interfaces objects.
There is also limited CRUD from the SDataEntityRepository class.

USE
The provider and sample app are based on 7.5.2.  You must have a 7.5.2 SData portal to use it.
Assembly references are retrieved from the SalesLogix assembly references folder.
You need to put a copy of the generated Sage.Integration.Entity.Feeds.dll in your assembly references folder.
You need to place copies of the generated concrete entities in the sample client's bin folder to use the sample.

LIMITATIONS AND BUGS
Only the Where and Select linq query methods are implemented
not all query operators are implemented
There is no relationship support at all
calling code snippet properties that use NHibernate will not work
Include support that is currently part of the API does not work
There is no error handling
Updates are broken
Paging is not implemented (only the first page is returned)
There is a typecast error executing a select query that does not return the querying entity when there is no where clause
ETags aren't captured after inserts and updates, so a get (select query or GetById) is required before executing an update or delete.
