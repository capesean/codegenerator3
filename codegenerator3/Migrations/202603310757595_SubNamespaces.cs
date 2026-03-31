namespace WEB.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class SubNamespaces : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.Projects", "Namespace", c => c.String(nullable: false, maxLength: 20));
            AddColumn("dbo.Projects", "CoreSubNamespace", c => c.String(nullable: false, maxLength: 20));
            AddColumn("dbo.Projects", "WebSubNamespace", c => c.String(nullable: false, maxLength: 20));
            Sql(@"update projects 
set namespace = case when charindex('.', modelsnamespace) = 0 then modelsnamespace else left(modelsnamespace, charindex('.', modelsnamespace)-1) end,
	CoreSubNamespace = case when name = 'CYBIL' then 'Core' else 'Web' end,
	WebSubNamespace = 'Web'
");
        }
        
        public override void Down()
        {
            DropColumn("dbo.Projects", "WebSubNamespace");
            DropColumn("dbo.Projects", "CoreSubNamespace");
            DropColumn("dbo.Projects", "Namespace");
        }
    }
}
