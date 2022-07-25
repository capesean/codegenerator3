namespace WEB.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class collectionSingular : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.Relationships", "CollectionSingular", c => c.String(nullable: false, maxLength: 50));
        }
        
        public override void Down()
        {
            DropColumn("dbo.Relationships", "CollectionSingular");
        }
    }
}
