DESCRIPTION	
This is an early version of a linq to SData provider.
It allows you to write linq queries in .Net, which get translated into SData queries, sent to an SData portal and returned as Sage.Entity.Interfaces objects.
There is also limited CRUD from the SDataEntityRepository class.

USE
The provider and sample app are based on 7.5.2.  You must have a 7.5.2 SData portal to use it.
Assembly references are retrieved from the SalesLogix assembly references folder.
CustomBuild.AdminModule.csproj is an AA admin module used to build entity classes that implement Sage.Entity.Interfaces that are suitable to be used with SData.
You should modify the build event in CustomBuild.AdminModule.csproj to copy itself out to where you run your application architect.
Copy the included code template, Default-SDataClientEntity-SalesLogix.SDataClientEntity.codetemplate.xml, to your model (\Model\Entity Model\CodeTemplates\Entity\)
Copy the built SDataLinqProvider.dll to your SalesLogix reference assemblies folder
To build these new entities: from the build menu, choose Packages, and then "SData Client Entities"
Copy the built assembly Sage.SData.Client.Entities.dll from \Model\deployment\webroot\common\bin\ to your client application's bin debug folder

LIMITATIONS AND BUGS
Only the Where and Select linq query methods are implemented
not all query operators are implemented
There is no relationship support at all
calling code snippet properties does not work
Include support that is currently part of the API does not work
There is no error handling
Updates are broken
ETags aren't captured after inserts and updates, so a get (select query or GetById) is required before executing an update or delete.
