lexer grammar CEvolLexer;

// Ключевые слова
NAMESPACE: 'namespace';
USING: 'using';
CLASS    : 'class';
IF       : 'if';
ELSE     : 'else';
WHILE    : 'while';
RETURN   : 'return';
LOC  : 'loc';
NEW : 'new';
CONSTRUCTOR : 'ctor';
DESTRUCTOR : 'dtor';
REF : 'ref';
REFB : 'refb';

// Операторы и знаки
ASSIGN   : '=';
PLUS     : '+';
MINUS    : '-';
MUL      : '*';
DIV      : '/';
BIT_AND  : '&';
BIT_OR   : '|';
AND  : '&&';
OR   : '||';
BIT_XOR  : '^';
EQ       : '==';
NEQ      : '!=';
LT       : '<';
GT       : '>';
LPAREN   : '(';
RPAREN   : ')';
LBRACE   : '{';
RBRACE   : '}';
LBRACK : '[' ;
RBRACK : ']' ;
SEMICOLON: ';';
COMMA : ',';
DOT : '.' ;

// Модификаторы
PUBLIC : 'public';
PRIVATE : 'private';
STATIC : 'static';
READONLY : 'readonly';
EXTERN : 'extern';
INFARGS : 'infargs';

// Идентификаторы и литералы
IDENTIFIER : [a-zA-Z_][a-zA-Z0-9_]*;
NUMBER     : [0-9]+('.'[0-9]+)?;
WS         : [ \t\r\n]+ -> skip;
STRING     : '"' ( '\\' . | ~["\\\r\n] )* '"';

LINE_COMMENT
    : '//' ~[\r\n]* -> channel(HIDDEN)
    ;