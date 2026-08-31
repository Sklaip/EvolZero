parser grammar CEvolParser;

options { tokenVocab = CEvolLexer; }

// --- Точка входа ---
program : namespaceDecl usingDecl? (classDecl | functionDecl | fieldDecl | abstractFunctionDecl)* EOF;

namespaceDecl : NAMESPACE IDENTIFIER SEMICOLON ;
usingDecl : USING IDENTIFIER SEMICOLON ;

// --- Модификаторы ---
// Модификаторы доступа: public, private (опционально)
accessModifier : PUBLIC | PRIVATE ;

// Дополнительные модификаторы: static, readonly (в любом количестве и порядке)
extraModifier : STATIC | READONLY | EXTERN | INFARGS;

qualifier : REF | REFB;

// --- Типы данных ---

arraySpec : LBRACK expression? RBRACK ;
typeSpec : (qualifier)* IDENTIFIER arraySpec* ;


// --- Объявления ---
// Модификаторы + Тип + Имя
fieldDecl : accessModifier? extraModifier* typeSpec IDENTIFIER (LPAREN args? RPAREN)? (ASSIGN expression)? SEMICOLON ;

// Функция/Метод
functionDecl : accessModifier? extraModifier* typeSpec IDENTIFIER LPAREN params? RPAREN block ;
abstractFunctionDecl : accessModifier? extraModifier* typeSpec IDENTIFIER LPAREN params? RPAREN SEMICOLON ;
constructorDecl : accessModifier? extraModifier* CONSTRUCTOR LPAREN params? RPAREN block ;
desctructorDecl : accessModifier? extraModifier* DESTRUCTOR LPAREN params? RPAREN block ;

params : typeSpec IDENTIFIER (COMMA typeSpec IDENTIFIER)* ;

// --- Инструкции ---
classDecl : CLASS IDENTIFIER LBRACE (fieldDecl | functionDecl | abstractFunctionDecl | constructorDecl | desctructorDecl)* RBRACE ;

block : LBRACE statement* RBRACE ;

statement 
    : fieldDecl                      # VarDeclStmt
    | assignment SEMICOLON           # AssignStmt
    | ifStatement                    # IfStmt
    | whileStatement                 # WhileStmt
    | RETURN expression SEMICOLON    # ReturnStmt
    | block                          # BlockStmt
    | expression SEMICOLON           # ExprStmt
    ;

assignment : (qualifier)? expression ASSIGN expression ;

ifStatement : IF LPAREN expression RPAREN statement (ELSE statement)? ;

whileStatement : WHILE LPAREN expression RPAREN block ;

// --- Выражения ---
// TODO: сделать чтобы синтаксически как к функции можно было обратиться к любому выражению. Сейчас () можно сделать четко после IDENTIFIER
// TODO: так же сейчас у NEW и REF разные приоритеты. Хз, но мнеп кажется так быть не должно
expression 
    : MINUS? NUMBER                              # NumberExpr
    | STRING                                     # StringExpr
    | IDENTIFIER                                 # IdExpr
    | LPAREN expression RPAREN                   # ParenExpr

    | NEW typeSpec ( LPAREN args? RPAREN )?    # NewExpr

    | IDENTIFIER LPAREN args? RPAREN             # CallExpr
    | expression LBRACK args RBRACK              # IndexExpr
    | expression DOT IDENTIFIER ( LPAREN args? RPAREN )? # MemberAccess

    | LPAREN typeSpec RPAREN expression # CastExpr

    | LOC expression                             # LocExpr
    | REF expression                             # RefExpr

    | expression (MUL | DIV) expression          # MulDivExpr
    | expression (PLUS | MINUS) expression       # AddSubExpr
    | expression (LT | GT) expression # LtGtExpr
    | expression (EQ | NEQ) expression # EqNeqExpr
    | expression BIT_AND expression # BitAndExpr
    | expression BIT_XOR expression # BitXorExpr
    | expression BIT_OR expression # BitOrExpr
    | expression AND expression # LogicalAndExpr
    | expression OR expression # LogicalOrExpr
    ;

args : expression (COMMA expression)* ;