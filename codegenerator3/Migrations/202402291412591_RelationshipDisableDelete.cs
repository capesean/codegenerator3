namespace WEB.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class RelationshipDisableDelete : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.Relationships", "DisableDelete", c => c.Boolean(nullable: false));
        }
        
        public override void Down()
        {
            DropColumn("dbo.Relationships", "DisableDelete");
        }
    }
}
