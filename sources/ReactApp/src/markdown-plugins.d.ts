declare module "@adobe/remark-gridtables" {
  import type { Plugin } from "unified";

  const remarkGridTables: Plugin<[]>;
  export default remarkGridTables;
}

declare module "@adobe/mdast-util-gridtables" {
  import type { Handler } from "mdast-util-to-hast";

  export function mdast2hastGridTablesHandler(options?: { noHeader?: boolean }): Handler;
}
