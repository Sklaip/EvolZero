Существует два вида ссылок - вледющая и заимствованная (refo и ref соответственно).
Владеющая ссылка может быть только одна, на нее может приходиться несколько заимствованных ссылок

1) Передача владения:
	1. Если одну владующую ссылку (А) присвоить в другую (Б), то ссылка Б считается деинициализированной
	2. Если функция принимает как аргумент владеющую ссылку, то после вызова функции переданная ссылка считается деинициализированной (владению ушло в функцию)

2) Для владеющей ссылки может быть вызвано удаление, для заимствованной - нет.

3) Условия при которых удаляется объект, на который ссылается владеющая ссылка:
	3. Ссылка вышла из зоны видимости (блок кода закончился)
	4. Во владующую ссылку присвоен новый объект (старый объект будет удален)
	5. Если владение ссылкой было отдано хотя бы в одном из ветвлений блока if/if-else/else, то во всех остальных ветвлениях она удаляется 
		то есть компилятор к передачи владения внутри if/if-else/else относистя консервативно - если хотя бы в одном условии ссылка деинициализируется,
		то на выходе из всего if/if-else/else блока мы к ней должны относиться как к деинициализированной. А чтобы не было утечек памяти, 
		в блоках где владение все же отдано не было, мы эту ссылку удаляем. К ветвлению в котором произошла передача владения, компилятор относится позитивно - 
		владеющая ссылка не исчезла, она просто поменялась, а значит утечки памяти не будет, достаточно просто запретить доступ к старой ссылке. 
	6. Было вызвано удаление родительского объекта (если ссылка Б является полем объекта А, то при удалении А, будет вызыванои удаление Б)

4) Возможность изменения времени жизни ссылок:
	1. У владеющих ссылок, переданных через аргументы, по умолчанию время жизни считается равным блоку функции. То есть при выходе из функции ссылка удаляется
	2. Объект с владеющей ссылкой А с временем жизни А1 можно передать в ссылку Б с временем жизни Б1, даже если Б1 < А1 (но с ограничениями описанными ниже)
	3. Владеющую ссылку можно вернуть из функции через return. Тогда ее время жизни будет равно времени жизни перменной, которая получает результат работы функции
	4. В случае если мы говорим о методе класса, то владеющая ссылка переданная через аргумент может быть присвоена в поле текущего объекта, 
		если мы явно указали в блоке lifetimes что ссылка живет столько же, сколько живет объект

5) Связанные ссылки и алиасы:
	1. При некоторых условиях ссылки могут стать связанными. Времена жизни связанных ссылок должны быть одинаковыми, 
		попытка уменьшить или увеличить время жизни одного из объектов приведет к ошибке
	2. Если мы присваеваем владеющую ссылку в поле объекта с типом заимствованной ссылки (поле с типо ref), то объект становится алиасом владеющей ссылки
	3. Время жизни аргумента фукнции с типом заимствованной ссылки считает неизвестным, то есть попытка присвоить эту ссылку в поле объекта, созданного внутри функции, приведет к ошибке 
		несоответсвия времен жизни ссылок
	4. Присваивание владеющей ссылки в поле объекта с типом заимствованной ссылки означает связывание этих двух ссылок
	5. Взятие заимствованной ссылки с поля объекта с типом владеющей ссылки считается созданием алиаса на объект. Эта ссылка становится еще одним алиасом на объект

6) Ограничения:
	1. Нельзя передавать владение ссылкой, у которой есть активные алиасы или связанные ссылки
	2. Заимствованная ссылка не может жить дольше владеющей ссылки
	3. Объект с владеющей ссылки А с временем жизни А1 можно передать в ссылку Б с временем жизни Б1, даже если Б1 < А1, но только в случае если А не имеет алиасов и связанных ссылок
		(в случае наличия связанных ссылок это допустимо, если меняется время жизни сразу всех ссылок. Например путем передачи этих ссылок 
		в функцию с явно прописанными lifetimes для эти аргументов, и эти lifetime'ы должны быть одинаковыми)
	4. Если у объекта есть дочерние владеющие ссылки (поля с типом refo), то время жизни этих объектов всегда должно быть равно времени жизни родителя
		эти ссылки можно переприсваивать только в том случае, если время жизни предыдущей ссылки изменено не будет. Это делается путем "обмена": 
		мы с помощью оператора <- устанавливаем полю новую ссылку, сразу же получаем старую и должны ее присвоить в переменную, которая имеет такое же время жизни как 
		и у объекта, с которого была снята ссылка. После этого эта перменная и объект считаются связанными ссылками. Их время жизни всегда должно быть одинаковым

7) Ключевое слово lifetimes
	1. lifetimes это специальный конструкт, в котором могут прописываться ограничения и расширения времен жизни. lifetimes принадлежит сигнатуре функции.
	2. Эти правила можно прописывать для refo и ref аргументов, для this и для return. Првила пишутеся путем связки времен жизни.
		Например объявление метода: Method(refo SomeType arg1) lifetimes(arg1 ~ this) означает что теперь время жизни ссылки arg1 должно быть равно времени жизни this.
		Мы обязаны присвоить этот аргумент в одно из полей текущго класса
	3. Если мы пишем return в lifetimes то это означает что мы говорим о возвращаемом объекте.
	4. Описание ref аргумента внутри lifetimes приводит к тому, что компилятор больше не считает время жизни этой ссылки неизвестным, оно становится равным тому, что мы сами указали
	5. ref может быть связано с return, refo - нет
	6. return может быть связан с this только в случае если мы возвращаем ref


Пример кода:
	ctor - означает конструктор, dtor - деструктор. Все ссылки разыменовываются автоматически. То есть если мы делаем var1 = var2 и при этом var2 является ссылкой, 
то из-за автоматического разыменования в var1 будет не новая ссылка, а значение, на которое указывает var2. Для того чтобы изменить саму ссылку нужно дополнительно писать refo (или ref).

```csharp
namespace Program;

extern infargs int printf(ref byte ptr);
extern infargs int scanf(ref byte ptr);

class Structure 
{
	public int Num;

	public ctor(int num)
	{
		Num = num;
	}

	public dtor()
	{
		printf(ref "Structure destructor. Num: ");
		printf(ref "%d\n", Num);
	}
}

class TestObj
{
	public int ObjectNum;
	public refo Structure Data;
	public ref Structure BorrowData;

	public ctor(refo Structure data, int objectNum) lifetimes (data ~ this)
	{
		refo Data = data;
		ref BorrowData = Data;
		ObjectNum = objectNum;
	}

	public ctor(int objectNum)
	{
		refo Data = new Structure(300);
		ref BorrowData = Data;
		ObjectNum = objectNum;
	}

	public int SumStructuresNums()
	{
		return Data.Num + BorrowData.Num;
	}

	public dtor()
	{
		printf(ref "\n");
		printf(ref "TestObj destructor. ObjectNum: ");
		printf(ref "%d\n", ObjectNum);
		printf(ref "  Data.Num: ");
		printf(ref "%d\n", Data.Num);
		printf(ref "  BorrowData.Num: ");
		printf(ref "%d\n", BorrowData.Num);
		printf(ref "\n");
	}
}

void PassOwnerRef(refo TestObj ownering)
{
	int sum = ownering.SumStructuresNums();
	printf(ref "ownering sum: ");
	printf(ref "%d\n", sum);
}

void PassBorrowRef(ref TestObj borrow)
{
	int sum = borrow.SumStructuresNums();
	printf(ref "borrow sum: ");
	printf(ref "%d\n", sum);
}

void Test(refo TestObj outsideObj)
{
	refo Structure structureStub = new Structure(10);
	ref outsideObj.BorrowData = structureStub;

	int num = 0;
    scanf(ref "%d", loc num);

	refo TestObj obj = new TestObj(new Structure(2), 2);

	refo Structure structureOne = new Structure(21);
	refo Structure strcRec = new Structure(57);

	if (num == 1) 
	{
		printf(ref "num is 1\n");
		ref obj.BorrowData = structureStub;
		refo strcRec = obj.Data <- structureOne;
		// тут strcRec должен удалиться, structureOne деинциализируется
		printf(ref "Test function\n");
		// тут должен удалиться obj, тк в блоке else if мы его передаем в PassOwnerRef (PassOwnerRef его удалит, тк получил владение)
		// а значит obj должен быть удален и во всех остальных ветках блока if/else-if/else
	}
	else if (num == 2)
	{
		printf(ref "num is 2\n");
		ref obj.BorrowData = structureOne;
		PassOwnerRef(refo obj);
		printf(ref "Test function\n");
		//тут structureOne должен удалиться, тк в блоке if мы эту переменную деинциализировали через оператор <-
	} 
	else
	{
		printf(ref "num is 3\n");
		ref obj.BorrowData = strcRec;
		PassBorrowRef(ref obj);
		printf(ref "Test function\n");
		//тут structureOne должен удалиться, тк в блоке if мы эту переменную деинциализировали через оператор <-
		// тут должен удалиться obj
	}

	printf(ref "End function\n");
}


int main()
{
	Test(new TestObj(1));

	return 0;
}
```

Вывод будет следующим:

При вводе 1:
num is 1
Structure destructor. Num: 57
Test function

TestObj destructor. ObjectNum: 2
  Data.Num: 21
  BorrowData.Num: 10

Structure destructor. Num: 21
End function

TestObj destructor. ObjectNum: 1
  Data.Num: 300
  BorrowData.Num: 10

Structure destructor. Num: 300
Structure destructor. Num: 10
Structure destructor. Num: 2

При вводе 2:
num is 2
ownering sum: 23

TestObj destructor. ObjectNum: 2
  Data.Num: 2
  BorrowData.Num: 21

Structure destructor. Num: 2
Test function
Structure destructor. Num: 21
End function

TestObj destructor. ObjectNum: 1
  Data.Num: 300
  BorrowData.Num: 10

Structure destructor. Num: 300
Structure destructor. Num: 10
Structure destructor. Num: 57

при вводе 3:
num is 3
borrow sum: 59
Test function
Structure destructor. Num: 21

TestObj destructor. ObjectNum: 2
  Data.Num: 2
  BorrowData.Num: 57

Structure destructor. Num: 2
End function

TestObj destructor. ObjectNum: 1
  Data.Num: 300
  BorrowData.Num: 10

Structure destructor. Num: 300
Structure destructor. Num: 10
Structure destructor. Num: 57