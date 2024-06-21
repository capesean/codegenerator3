namespace WEB.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class RemoveUseSelectorDirective : DbMigration
    {
        public override void Up()
        {
            DropColumn("dbo.Relationships", "UseSelectorDirective");
        }
        
        public override void Down()
        {
            AddColumn("dbo.Relationships", "UseSelectorDirective", c => c.Boolean(nullable: false));
        }
    }
}
