namespace WEB.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class namespaces : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.Projects", "ModelsNamespace", c => c.String(nullable: false, maxLength: 20));
            AddColumn("dbo.Projects", "WebNamespace", c => c.String(nullable: false, maxLength: 20));
            Sql("UPDATE Projects SET ModelsNamespace = NameSpace, WebNameSpace = NameSpace");
        }
        
        public override void Down()
        {
            DropColumn("dbo.Projects", "WebNamespace");
            DropColumn("dbo.Projects", "ModelsNamespace");
        }
    }
}
